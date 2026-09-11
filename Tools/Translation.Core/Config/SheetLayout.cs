using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 번역 시트 한 장을 어떻게 읽을지. 셀 격자를 TranslateEntry 로 바꾼다 </summary>
    public sealed class SheetLayout
    {
        public string SheetName { get; }

        /// <summary> 이 시트가 담는 대상들. 시트 하나에 표가 여럿 올 수 있다 </summary>
        public IReadOnlyList<TableTarget> Targets { get; }

        /// <summary> 대상이 하나인 시트에서 쓴다 </summary>
        public TableTarget Target
        {
            get { return Targets.Count > 0 ? Targets[0] : null; }
        }

        public IKeyCodec KeyCodec { get; }

        public IReadOnlyList<string> LanguageIds { get; }

        /// <summary> 헤더 다음에 건너뛸 행 수. 2행이 타입 행이면 1 </summary>
        public int DataRowOffset { get; }

        public SheetLayout(
            string sheetName,
            TableTarget target,
            IKeyCodec keyCodec,
            IReadOnlyList<string> languageIds,
            int dataRowOffset = 0)
            : this(sheetName, new[] { target ?? throw new ArgumentNullException(nameof(target)) },
                keyCodec, languageIds, dataRowOffset)
        {
        }

        public SheetLayout(
            string sheetName,
            IReadOnlyList<TableTarget> targets,
            IKeyCodec keyCodec,
            IReadOnlyList<string> languageIds,
            int dataRowOffset = 0)
        {
            if (string.IsNullOrEmpty(sheetName))
                throw new TranslationConfigException("sheetName은 필수입니다.");

            if (targets == null || targets.Count == 0)
                throw new TranslationConfigException(sheetName + ": 대상이 하나도 없습니다.");

            SheetName = sheetName;
            Targets = targets;
            KeyCodec = keyCodec ?? throw new ArgumentNullException(nameof(keyCodec));
            LanguageIds = languageIds ?? throw new ArgumentNullException(nameof(languageIds));
            DataRowOffset = Math.Max(0, dataRowOffset);
        }

        /// <summary> 이 레이아웃이 시트에서 찾는 컬럼 이름들. 진단용 </summary>
        public IReadOnlyList<string> ExpectedColumns
        {
            get
            {
                var columns = new List<string>(KeyCodec.KeyColumns.Count + LanguageIds.Count);
                columns.AddRange(KeyCodec.KeyColumns);
                columns.AddRange(LanguageIds);
                return columns;
            }
        }

        /// <summary>
        /// 행들을 TranslateEntry 로 바꾼다. rows[0] 은 헤더이고 컬럼은 이름으로 찾는다.
        /// 모르는 컬럼은 무시하고, 해석하지 못한 행은 warnings 에 남긴다
        /// </summary>
        public List<TranslateEntry> ReadEntries(IReadOnlyList<IReadOnlyList<string>> rows, IList<string> warnings)
        {
            var entries = new List<TranslateEntry>();
            if (rows == null || rows.Count == 0)
                return entries;

            var header = rows[0];
            var keyIndices = new int[KeyCodec.KeyColumns.Count];
            for (var i = 0; i < keyIndices.Length; i++)
            {
                keyIndices[i] = IndexOf(header, KeyCodec.KeyColumns[i]);
                if (keyIndices[i] < 0)
                {
                    Warn(warnings, SheetName + ": 키 컬럼 " + KeyCodec.KeyColumns[i] + "을 헤더에서 찾을 수 없습니다.");
                    return entries;
                }
            }

            var langIndices = new int[LanguageIds.Count];
            for (var i = 0; i < langIndices.Length; i++)
            {
                langIndices[i] = IndexOf(header, LanguageIds[i]);
                if (langIndices[i] < 0)
                    Warn(warnings, SheetName + ": 언어 컬럼 " + LanguageIds[i] + "이 없습니다. 빈 값으로 읽습니다.");
            }

            var context = KeyDecodeContext.FromTargets(Targets);
            var dataStart = 1 + DataRowOffset;
            var seen = new HashSet<TranslateKey>();

            for (var r = dataStart; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null || IsBlank(row))
                    continue;

                var keyCells = new string[keyIndices.Length];
                for (var i = 0; i < keyIndices.Length; i++)
                    keyCells[i] = Cell(row, keyIndices[i]);

                if (!KeyCodec.TryDecode(keyCells, context, out var key))
                {
                    Warn(warnings, SheetName + " 행 " + (r + 1) + ": 키를 해석할 수 없습니다: " + string.Join("|", keyCells));
                    continue;
                }

                if (!seen.Add(key))
                {
                    Warn(warnings, SheetName + " 행 " + (r + 1) + ": 키가 중복입니다: " + key);
                    continue;
                }

                var entry = new TranslateEntry { Key = key };
                for (var i = 0; i < langIndices.Length; i++)
                {
                    if (langIndices[i] >= 0)
                        entry.Set(LanguageIds[i], Cell(row, langIndices[i]));
                }

                entries.Add(entry);
            }

            return entries;
        }

        private static int IndexOf(IReadOnlyList<string> header, string name)
        {
            if (header == null || string.IsNullOrEmpty(name))
                return -1;

            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals((header[i] ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string Cell(IReadOnlyList<string> row, int index)
        {
            if (index < 0 || row == null || index >= row.Count)
                return string.Empty;

            return row[index] ?? string.Empty;
        }

        private static bool IsBlank(IReadOnlyList<string> row)
        {
            for (var i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrEmpty(row[i]) && row[i].Trim().Length != 0)
                    return false;
            }

            return true;
        }

        private static void Warn(IList<string> warnings, string message)
        {
            warnings?.Add(message);
        }
    }
}
