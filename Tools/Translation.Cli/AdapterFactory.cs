using System;
using System.Collections.Generic;
using System.IO;
using Translation.Excel;
using Translation.GoogleSheets;

namespace Translation.Cli
{
    /// <summary> 설정 파일의 translations · gameData · sync 섹션을 보고 어댑터와 옵션을 만든다 </summary>
    internal static class AdapterFactory
    {
        /// <param name="writable"> sync 용. 구글 시트는 쓰기 스코프로 인증한다 </param>
        public static ITranslationSource CreateTranslationSource(
            TranslationConfigDocument document,
            out string label,
            out IDisposable owned,
            bool writable = false)
        {
            // translations 가 없으면 설정 파일 옆의 CSV 시트다
            var section = document.Section("translations") ?? new Dictionary<string, object>
            {
                { "type", "csv" },
                { "path", "." },
            };

            var type = TypeOf(section);
            owned = null;

            switch (type)
            {
                case "csv":
                {
                    var source = new CsvTranslationSource(Resolve(MiniJson.GetString(section, "path", "."), document));
                    label = source.Label;
                    return source;
                }

                case "excel":
                {
                    var source = new ExcelTranslationSource(RequirePath(section, document));
                    owned = source;
                    label = source.Label;
                    return source;
                }

                case "googlesheets":
                {
                    var spreadsheetId = MiniJson.GetString(section, "spreadsheetId");
                    if (string.IsNullOrEmpty(spreadsheetId))
                        throw new TranslationConfigException(
                            "googleSheets 섹션에 spreadsheetId가 필요합니다. 시트 URL을 그대로 넣어도 됩니다.");

                    var credentials = MiniJson.GetString(section, "credentials");
                    if (string.IsNullOrEmpty(credentials))
                        throw new TranslationConfigException("googleSheets 섹션에 credentials 경로가 필요합니다.");

                    var service = GoogleSheetsAuth.Create(Resolve(credentials, document), writable);
                    var source = new GoogleSheetsTranslationSource(new SheetsClient(service, spreadsheetId));
                    label = source.Label;
                    return source;
                }

                default:
                    throw new TranslationConfigException("translations.type을 알 수 없습니다: " + type);
            }
        }

        /// <summary> gameData 섹션 — sync 가 원문을 읽을 게임 DB </summary>
        public static IGameDataSource CreateGameDataSource(
            TranslationConfigDocument document,
            out IDisposable owned)
        {
            var section = document.Section("gameData");
            if (section == null)
                throw new TranslationConfigException(
                    "설정에 gameData 섹션이 없습니다. sync 가 원문을 읽을 게임 DB 위치를 지정하세요.");

            var type = TypeOf(section);
            var offset = MiniJson.GetInt(section, "dataRowOffset");
            owned = null;

            switch (type)
            {
                case "json":
                    return new JsonGameDataSource(RequirePath(section, document));

                case "csv":
                    return new CsvGameDataSource(RequirePath(section, document)) { DataRowOffset = offset };

                case "excel":
                {
                    var source = new ExcelGameDataSource(RequirePath(section, document)) { DataRowOffset = offset };
                    owned = source;
                    return source;
                }

                default:
                    throw new TranslationConfigException("gameData.type은 json · csv · excel 중 하나여야 합니다: " + type);
            }
        }

        /// <summary> sync 섹션 — 마커와 상태 컬럼. 없으면 기본값 </summary>
        public static SyncOptions CreateSyncOptions(TranslationConfigDocument document, bool prune, bool dryRun)
        {
            var section = document.Section("sync") ?? new Dictionary<string, object>();
            var options = new SyncOptions { Prune = prune, DryRun = dryRun };

            options.ChangedMarker = MiniJson.GetString(section, "changedMarker", options.ChangedMarker);
            options.SuggestedMarker = MiniJson.GetString(section, "suggestedMarker", options.SuggestedMarker);
            options.StateColumn = MiniJson.GetString(section, "stateColumn", options.StateColumn);
            options.StateNew = MiniJson.GetString(section, "stateNew", options.StateNew);
            options.StateChanged = MiniJson.GetString(section, "stateChanged", options.StateChanged);
            options.StateRemoved = MiniJson.GetString(section, "stateRemoved", options.StateRemoved);
            return options;
        }

        public static BundleBuildOptions CreateBundleOptions(
            TranslationConfigDocument document,
            string version,
            string sourceLabel)
        {
            // 기본: Unity 가 TextAsset 으로 읽는 .json, 빌드 동봉용 LocalKey.csv 함께
            var section = document.Section("output");
            var options = new BundleBuildOptions
            {
                BundleVersion = version,
                SourceLabel = sourceLabel,
                BakeFallback = MiniJson.GetBool(section, "bakeFallback", true),
                IndentJson = MiniJson.GetBool(section, "indentJson"),
                FileExtension = MiniJson.GetString(section, "fileExtension", ".json"),
                CsvFileName = MiniJson.GetString(section, "csvFileName", LocalKeyCsv.DefaultFileName),
                ManifestFileName = MiniJson.GetString(section, "manifestFileName"),
            };

            var compression = MiniJson.GetString(section, "compression", "none");
            if (!Enum.TryParse<BundleCompression>(compression, true, out var parsed))
                throw new TranslationConfigException("output.compression은 none 또는 gzip만 지원합니다: " + compression);

            options.Compression = parsed;
            return options;
        }

        /// <summary> script 섹션. 없으면 false. directory 는 설정 파일 기준으로 푼다 </summary>
        public static bool ScriptOutput(TranslationConfigDocument document, out string directory, out string ns)
        {
            directory = null;
            ns = null;

            var section = document.Section("script");
            if (section == null)
                return false;

            var raw = MiniJson.GetString(section, "directory");
            if (string.IsNullOrEmpty(raw))
                throw new TranslationConfigException("script 섹션에 directory 가 필요합니다 (LocalKey.cs 를 넣을 폴더).");

            directory = Resolve(raw, document);
            ns = MiniJson.GetString(section, "namespace", LocalKeyScript.DefaultNamespace);
            return true;
        }

        public static string OutputDirectory(TranslationConfigDocument document, string overridePath)
        {
            if (!string.IsNullOrEmpty(overridePath))
                return overridePath;

            // output.directory 가 없으면 설정 파일 옆 Bundles/
            var section = document.Section("output");
            return Resolve(MiniJson.GetString(section, "directory", "./Bundles"), document);
        }

        private static string TypeOf(Dictionary<string, object> section)
        {
            var type = MiniJson.GetString(section, "type");
            if (string.IsNullOrEmpty(type))
                throw new TranslationConfigException("섹션에 type이 없습니다. csv · excel · googleSheets (gameData 는 json · csv · excel) 중 하나여야 합니다.");

            return type.ToLowerInvariant();
        }

        private static string RequirePath(Dictionary<string, object> section, TranslationConfigDocument document)
        {
            var path = MiniJson.GetString(section, "path");
            if (string.IsNullOrEmpty(path))
                throw new TranslationConfigException("섹션에 path가 필요합니다.");

            return Resolve(path, document);
        }

        /// <summary> 상대 경로는 설정 파일 위치를 기준으로 푼다 </summary>
        private static string Resolve(string path, TranslationConfigDocument document)
        {
            if (Path.IsPathRooted(path) || string.IsNullOrEmpty(document.Path))
                return path;

            var root = Path.GetDirectoryName(Path.GetFullPath(document.Path));
            return string.IsNullOrEmpty(root) ? path : Path.GetFullPath(Path.Combine(root, path));
        }
    }
}
