using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Translation.Unity.Editor
{
    /// <summary>
    /// 설정 에셋으로 sync · validate · export 를 Unity 안에서 돌린다.
    /// 게임 DB 는 JSON · CSV, 번역 시트는 CSV 만 된다 (엑셀 · 구글 시트는 exe 로)
    /// </summary>
    public static class TranslationEditorRunner
    {
        public static SyncResult Sync(TranslationSettings settings, bool prune = false, bool dryRun = false)
        {
            var document = LoadDocument(settings);
            var gameDataDirectory = settings.GameDataDirectory;
            if (string.IsNullOrEmpty(gameDataDirectory))
                throw new TranslationConfigException("게임 DB 폴더를 지정하세요 (GameDataFolder).");

            IGameDataSource gameData = settings.GameDataFormat == TranslationSettings.GameDataType.Json
                ? new JsonGameDataSource(gameDataDirectory)
                : (IGameDataSource)new CsvGameDataSource(gameDataDirectory) { DataRowOffset = settings.DataRowOffset };

            var store = new CsvTranslationSource(settings.SheetDirectory);
            var options = new SyncOptions { Prune = prune, DryRun = dryRun };
            var result = new TranslationSynchronizer(document.Config).Sync(gameData, store, options);

            var sb = new StringBuilder();
            sb.AppendLine("[Translation] sync " + (dryRun ? "(dry-run) " : string.Empty) + gameData.Label + " → " + store.Label);
            foreach (var sheet in result.Sheets)
                sb.AppendLine("  " + sheet + (sheet.Written ? "  (씀)" : string.Empty));

            sb.AppendLine("신규 " + result.Added + " · 원문변경 " + result.Changed + " · 추천 " + result.Suggested
                          + " · 삭제됨 " + result.Removed + " · 지움 " + result.Pruned + " · 그대로 " + result.Unchanged);
            AppendWarnings(sb, result.Warnings);
            Debug.Log(sb.ToString());

            if (!dryRun)
                AssetDatabase.Refresh();

            return result;
        }

        public static ValidationReport Validate(TranslationSettings settings, bool strict = false)
        {
            var document = LoadDocument(settings);
            var source = new CsvTranslationSource(settings.SheetDirectory);
            var entries = new List<TranslateEntry>();
            var warnings = new List<string>();

            foreach (var sheetName in document.Config.SheetNames)
            {
                var layout = document.Config.GetLayout(sheetName);
                if (!source.SheetExists(layout.SheetName))
                {
                    warnings.Add("번역 시트가 없습니다: " + layout.SheetName);
                    continue;
                }

                var read = source.Read(layout, warnings);
                if (read != null)
                    entries.AddRange(read);
            }

            var report = new TranslationValidator(document.Config) { TreatMissingAsError = strict }.Validate(entries);

            var sb = new StringBuilder();
            sb.AppendLine("[Translation] validate " + source.Label);
            sb.AppendLine("검증 " + report);
            foreach (var pair in report.MissingByLanguage)
            {
                if (pair.Value > 0)
                    sb.AppendLine("  미번역 " + pair.Key + " " + pair.Value + "개");
            }

            foreach (var issue in report.Issues)
            {
                if (issue.Severity != IssueSeverity.Info)
                    sb.AppendLine("  " + issue);
            }

            AppendWarnings(sb, warnings);
            if (report.HasErrors)
                Debug.LogError(sb.ToString());
            else
                Debug.Log(sb.ToString());

            return report;
        }

        public static ExportResult Export(TranslationSettings settings, bool strict = false)
        {
            var document = LoadDocument(settings);
            var source = new CsvTranslationSource(settings.SheetDirectory);
            var options = new BundleBuildOptions
            {
                BundleVersion = settings.BundleVersion,
                SourceLabel = source.Label,
                BakeFallback = true,
                FileExtension = ".json",
                CsvFileName = LocalKeyCsv.DefaultFileName,
            };

            var result = new TranslationExporter(document.Config).Export(source, options);
            var directory = settings.OutputDirectory;

            var sb = new StringBuilder();
            sb.AppendLine("[Translation] export " + source.Label + " → " + directory);
            sb.AppendLine("검증 " + result.Validation);
            foreach (var issue in result.Validation.Issues)
            {
                if (issue.Severity == IssueSeverity.Error)
                    sb.AppendLine("  " + issue);
            }

            if (strict && result.Validation.HasErrors)
            {
                AppendWarnings(sb, result.Warnings);
                sb.AppendLine("검증 오류가 있어 파일을 쓰지 않았습니다.");
                Debug.LogError(sb.ToString());
                return result;
            }

            BundleBuilder.WriteToDirectory(result.Bundle, directory);
            sb.AppendLine("버전 " + result.Bundle.Manifest.BundleVersion);
            foreach (var language in result.Bundle.Manifest.Languages)
                sb.AppendLine("  " + language.Id + "  " + language.EntryCount + "개  " + language.File);

            sb.AppendLine("시트 " + result.SheetCount + "장, 엔트리 " + result.Entries.Count + "개");

            var scriptDirectory = settings.ScriptDirectory;
            if (!string.IsNullOrEmpty(scriptDirectory))
            {
                var written = LocalKeyScript.WriteFiles(
                    result.Entries, document.Config.Languages.SourceLanguageId,
                    settings.ScriptNamespace, scriptDirectory, result.Warnings);
                foreach (var path in written)
                    sb.AppendLine("코드 " + path);
            }

            AppendWarnings(sb, result.Warnings);
            Debug.Log(sb.ToString());

            AssetDatabase.Refresh();
            return result;
        }

        /// <summary> 설정 에셋 옆에 translation.config.json 과 bat 세 개를 쓴다. bat 은 이미 있으면 안 건드린다 </summary>
        public static string WriteConfigFiles(TranslationSettings settings)
        {
            var directory = settings.ConfigDirectory;
            Directory.CreateDirectory(directory);

            var configPath = settings.ConfigFilePath;
            File.WriteAllText(configPath, settings.ToConfigJson(), new UTF8Encoding(false));

            WriteIfMissing(Path.Combine(directory, TranslationScripts.SyncFileName), TranslationScripts.SyncBat);
            WriteIfMissing(Path.Combine(directory, TranslationScripts.ValidateFileName), TranslationScripts.ValidateBat);
            WriteIfMissing(Path.Combine(directory, TranslationScripts.ExportFileName), TranslationScripts.ExportBat);

            AssetDatabase.Refresh();
            Debug.Log("[Translation] 설정 파일을 썼습니다: " + configPath);
            return configPath;
        }

        /// <summary> 설정 에셋을 설정 문서로. 상대 경로는 에셋 폴더 기준이다 </summary>
        public static TranslationConfigDocument LoadDocument(TranslationSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var document = TranslationConfigFile.Parse(settings.ToConfigJson());
            document.Path = settings.ConfigFilePath;
            return document;
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static void AppendWarnings(StringBuilder sb, IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return;

            sb.AppendLine("경고 " + warnings.Count + "건");
            foreach (var warning in warnings)
                sb.AppendLine("  " + warning);
        }
    }
}
