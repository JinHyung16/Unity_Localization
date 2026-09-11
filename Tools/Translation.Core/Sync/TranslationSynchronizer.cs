using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary>
    /// 게임 DB 의 원문으로 번역 시트를 최신화한다. 시트 단위로 돈다.
    /// 새 키는 행을 붙이고, 원문이 바뀐 키는 번역 앞에 마커를 붙이고,
    /// 사라진 키는 상태 컬럼에 표시한다 (Prune 이면 지운다).
    /// 팀이 둔 다른 컬럼과 행 순서는 그대로 두고,
    /// 원문 테이블이 없는 대상은 건너뛰어 그 키를 사라진 것으로 보지 않는다
    /// </summary>
    public sealed class TranslationSynchronizer
    {
        private readonly TranslationConfig _config;

        public TranslationSynchronizer(TranslationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public SyncResult Sync(IGameDataSource gameData, ITranslationSheetStore store, SyncOptions options = null)
        {
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            options = options ?? new SyncOptions();
            var result = new SyncResult();

            foreach (var sheetName in _config.SheetNames)
            {
                var layout = _config.GetLayout(sheetName);
                result.Sheets.Add(SyncSheet(layout, gameData, store, options, result.Warnings));
            }

            return result;
        }

        private SheetSyncResult SyncSheet(
            SheetLayout layout,
            IGameDataSource gameData,
            ITranslationSheetStore store,
            SyncOptions options,
            List<string> warnings)
        {
            var sheet = new SheetSyncResult { SheetName = layout.SheetName };
            var sourceLang = _config.Languages.SourceLanguageId;

            // 1. 게임 DB 에서 원문을 모은다
            var desired = new List<KeyValuePair<TranslateKey, string>>();
            var desiredIndex = new Dictionary<TranslateKey, int>();
            var synced = new List<TableTarget>();

            foreach (var target in layout.Targets)
            {
                if (!Collect(target, layout.SheetName, gameData, sourceLang, desired, desiredIndex, warnings))
                {
                    sheet.SkippedTargets.Add(target.ToString());
                    continue;
                }

                synced.Add(target);
            }

            if (synced.Count == 0)
            {
                sheet.Skipped = true;
                return sheet;
            }

            // 2. 시트를 격자로 읽고 필요한 컬럼을 갖춘다
            sheet.Existed = store.SheetExists(layout.SheetName);
            var grid = sheet.Existed ? store.ReadGrid(layout.SheetName) : new List<string[]>();
            var rows = new List<List<string>>(grid.Count);
            foreach (var row in grid)
                rows.Add(new List<string>(row ?? new string[0]));

            if (rows.Count == 0)
                rows.Add(new List<string>());

            var header = rows[0];
            var headerChanged = false;
            var codec = layout.KeyCodec;
            var keyIndex = new int[codec.KeyColumns.Count];
            for (var i = 0; i < keyIndex.Length; i++)
            {
                keyIndex[i] = IndexOf(header, codec.KeyColumns[i]);
                if (keyIndex[i] >= 0)
                    continue;

                if (sheet.Existed && grid.Count > 0)
                    throw new TranslationConfigException(
                        "시트 " + layout.SheetName + " 에 키 컬럼 " + codec.KeyColumns[i]
                        + " 이 없습니다. 코덱 설정과 시트 헤더가 맞는지 확인하세요.");

                keyIndex[i] = header.Count;
                header.Add(codec.KeyColumns[i]);
                headerChanged = true;
            }

            var languageIds = layout.LanguageIds;
            var langIndex = new int[languageIds.Count];
            for (var i = 0; i < langIndex.Length; i++)
                langIndex[i] = Ensure(header, languageIds[i], ref headerChanged);

            var sourceIndex = Ensure(header, sourceLang, ref headerChanged);
            var stateIndex = string.IsNullOrEmpty(options.StateColumn)
                ? -1
                : Ensure(header, options.StateColumn, ref headerChanged);

            var dataStart = 1 + layout.DataRowOffset;
            while (rows.Count < dataStart)
                rows.Add(new List<string>());

            foreach (var row in rows)
                Pad(row, header.Count);

            // 3. 기존 행을 키로 찾는다
            var context = KeyDecodeContext.FromTargets(layout.Targets);
            var existing = new Dictionary<TranslateKey, int>();
            var cells = new string[keyIndex.Length];
            for (var r = dataStart; r < rows.Count; r++)
            {
                var empty = true;
                for (var i = 0; i < keyIndex.Length; i++)
                {
                    cells[i] = rows[r][keyIndex[i]];
                    if (!string.IsNullOrWhiteSpace(cells[i]))
                        empty = false;
                }

                if (empty)
                    continue;

                if (codec.TryDecode(cells, context, out var key) && !existing.ContainsKey(key))
                    existing[key] = r;
            }

            // 4. 같은 원문의 기존 번역 — 신규 행에 추천으로 넣는다
            var suggestions = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var pair in existing)
            {
                var row = rows[pair.Value];
                var source = row[sourceIndex];
                if (string.IsNullOrEmpty(source))
                    continue;

                for (var i = 0; i < langIndex.Length; i++)
                {
                    if (langIndex[i] == sourceIndex)
                        continue;

                    var value = row[langIndex[i]];
                    if (string.IsNullOrEmpty(value) || options.IsMarked(value))
                        continue;

                    if (!suggestions.TryGetValue(source, out var byLanguage))
                    {
                        byLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        suggestions[source] = byLanguage;
                    }

                    if (!byLanguage.ContainsKey(languageIds[i]))
                        byLanguage[languageIds[i]] = value;
                }
            }

            // 5. 신규 · 원문변경
            foreach (var pair in desired)
            {
                var key = pair.Key;
                var text = pair.Value;

                if (existing.TryGetValue(key, out var r))
                {
                    var row = rows[r];
                    if (string.Equals((row[sourceIndex] ?? string.Empty).Trim(), text.Trim(), StringComparison.Ordinal))
                    {
                        sheet.Unchanged++;
                        continue;
                    }

                    row[sourceIndex] = text;
                    for (var i = 0; i < langIndex.Length; i++)
                    {
                        if (langIndex[i] == sourceIndex)
                            continue;

                        var value = row[langIndex[i]];
                        if (string.IsNullOrEmpty(value) || options.IsMarked(value))
                            continue;

                        row[langIndex[i]] = options.ChangedMarker + value;
                    }

                    SetState(row, stateIndex, options.StateChanged);
                    sheet.Changed++;
                    continue;
                }

                var fresh = new List<string>(header.Count);
                Pad(fresh, header.Count);

                var encoded = codec.Encode(key);
                for (var i = 0; i < keyIndex.Length && i < encoded.Length; i++)
                    fresh[keyIndex[i]] = encoded[i] ?? string.Empty;

                fresh[sourceIndex] = text;

                var suggested = false;
                if (suggestions.TryGetValue(text, out var found))
                {
                    for (var i = 0; i < langIndex.Length; i++)
                    {
                        if (langIndex[i] == sourceIndex)
                            continue;

                        if (found.TryGetValue(languageIds[i], out var value))
                        {
                            fresh[langIndex[i]] = options.SuggestedMarker + value;
                            suggested = true;
                        }
                    }
                }

                SetState(fresh, stateIndex, options.StateNew);
                rows.Add(fresh);
                sheet.Added++;
                if (suggested)
                    sheet.Suggested++;
            }

            // 6. 사라진 키 — 이번에 원문을 읽은 대상의 키만 판정한다
            var keep = new bool[rows.Count];
            for (var i = 0; i < keep.Length; i++)
                keep[i] = true;

            foreach (var pair in existing)
            {
                if (desiredIndex.ContainsKey(pair.Key) || !Owns(synced, pair.Key))
                    continue;

                if (options.Prune)
                {
                    keep[pair.Value] = false;
                    sheet.Pruned++;
                    continue;
                }

                var row = rows[pair.Value];
                if (stateIndex >= 0 && !string.Equals(row[stateIndex], options.StateRemoved, StringComparison.Ordinal))
                    SetState(row, stateIndex, options.StateRemoved);

                sheet.Removed++;
            }

            // 7. 쓰기
            var dirty = sheet.HasChanges || (headerChanged && (sheet.Existed || sheet.Added > 0));
            if (!dirty || options.DryRun)
                return sheet;

            var output = new List<string[]>(rows.Count);
            for (var r = 0; r < rows.Count; r++)
            {
                if (keep[r])
                    output.Add(rows[r].ToArray());
            }

            store.WriteGrid(layout.SheetName, output);
            sheet.Written = true;
            return sheet;
        }

        /// <summary> 대상 하나의 원문을 모은다. 원문 테이블이 없으면 false </summary>
        private static bool Collect(
            TableTarget target,
            string sheetName,
            IGameDataSource gameData,
            string sourceLang,
            List<KeyValuePair<TranslateKey, string>> desired,
            Dictionary<TranslateKey, int> desiredIndex,
            List<string> warnings)
        {
            var table = target.ResolvedSourceTable;
            if (string.IsNullOrEmpty(table))
            {
                warnings.Add(sheetName + ": 독립 UI 세트에 sourceTable 이 없어 sync 를 건너뜁니다.");
                return false;
            }

            if (!gameData.TableExists(table))
            {
                warnings.Add(sheetName + ": 게임 DB 에 테이블이 없어 건너뜁니다: " + table + " (" + target + ")");
                return false;
            }

            IReadOnlyList<string> columns = target.IsStandalone
                ? new[] { string.IsNullOrEmpty(target.SourceColumn) ? sourceLang : target.SourceColumn }
                : target.Columns;

            var warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emptyIds = 0;

            foreach (var row in gameData.ReadTable(table))
            {
                var id = row.GetText(target.IdColumn).Trim();
                if (id.Length == 0)
                {
                    emptyIds++;
                    continue;
                }

                var subId = target.HasSubId ? row.GetText(target.SubIdColumn).Trim() : null;

                for (var c = 0; c < columns.Count; c++)
                {
                    var column = columns[c];
                    if (!row.Has(column))
                    {
                        if (warned.Add(column))
                            warnings.Add(table + ": 컬럼이 없습니다: " + column);

                        continue;
                    }

                    if (row.IsArray(column) && !target.IsArray)
                    {
                        if (warned.Add("[]" + column))
                            warnings.Add(table + "." + column + ": 배열 컬럼인데 arraySeparator 가 없어 건너뜁니다.");

                        continue;
                    }

                    var text = row.GetText(column, target.ArraySeparator);
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    var key = target.IsStandalone ? TranslateKey.Standalone(id) : target.MakeKey(column, id, subId);
                    if (desiredIndex.ContainsKey(key))
                    {
                        warnings.Add(table + ": 원문 키가 중복입니다: " + key);
                        continue;
                    }

                    desiredIndex[key] = desired.Count;
                    desired.Add(new KeyValuePair<TranslateKey, string>(key, text));
                }
            }

            if (emptyIds > 0)
                warnings.Add(table + ": " + target.IdColumn + " 이 빈 행 " + emptyIds + "개를 건너뜁니다.");

            return true;
        }

        private static bool Owns(List<TableTarget> targets, TranslateKey key)
        {
            foreach (var target in targets)
            {
                if (target.IsStandalone)
                {
                    if (key.IsStandalone)
                        return true;

                    continue;
                }

                if (key.IsStandalone || !string.Equals(target.Table, key.Table, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var column in target.Columns)
                {
                    if (string.Equals(column, key.Column, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static int Ensure(List<string> header, string name, ref bool changed)
        {
            var index = IndexOf(header, name);
            if (index >= 0)
                return index;

            header.Add(name);
            changed = true;
            return header.Count - 1;
        }

        private static int IndexOf(List<string> header, string name)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals((header[i] ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void Pad(List<string> row, int width)
        {
            for (var i = 0; i < row.Count; i++)
            {
                if (row[i] == null)
                    row[i] = string.Empty;
            }

            while (row.Count < width)
                row.Add(string.Empty);
        }

        private static void SetState(List<string> row, int stateIndex, string state)
        {
            if (stateIndex >= 0)
                row[stateIndex] = state ?? string.Empty;
        }
    }
}
