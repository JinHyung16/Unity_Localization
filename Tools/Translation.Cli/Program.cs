using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Translation.Cli
{
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitUsage = 1;
        private const int ExitFailed = 2;
        private const int ExitValidation = 3;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0)
                return Usage();

            var options = CliOptions.Parse(args, out var error);
            if (error != null)
            {
                Console.Error.WriteLine("인자 오류: " + error);
                return Usage();
            }

            try
            {
                switch (options.Command)
                {
                    case "init":
                        return Init(options);
                    case "export":
                        return Export(options);
                    case "validate":
                        return Validate(options);
                    case "sync":
                        return Sync(options);
                    case "diff":
                        return Diff(options);
                    case "help":
                        return Usage();
                    default:
                        Console.Error.WriteLine("알 수 없는 명령: " + options.Command);
                        return Usage();
                }
            }
            catch (TranslationConfigException e)
            {
                Console.Error.WriteLine("설정 오류: " + e.Message);
                return ExitFailed;
            }
            catch (TranslationBundleException e)
            {
                Console.Error.WriteLine("번들 오류: " + e.Message);
                return ExitFailed;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("실패: " + e.Message);
                if (options.Verbose)
                    Console.Error.WriteLine(e);

                return ExitFailed;
            }
        }

        private static int Init(CliOptions options)
        {
            var path = options.ConfigPath ?? TranslationConfigFile.DefaultFileName;
            if (File.Exists(path) && !options.Force)
            {
                Console.Error.WriteLine("이미 있습니다: " + path + " (--force로 덮어쓰기)");
                return ExitFailed;
            }

            File.WriteAllText(path, Templates.StarterConfig, new UTF8Encoding(false));
            Console.WriteLine("설정 파일을 만들었습니다: " + path);

            // 더블클릭으로 돌리는 bat. 이미 있으면 안 건드린다
            var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            WriteIfMissing(Path.Combine(directory, TranslationScripts.SyncFileName), TranslationScripts.SyncBat);
            WriteIfMissing(Path.Combine(directory, TranslationScripts.ValidateFileName), TranslationScripts.ValidateBat);
            WriteIfMissing(Path.Combine(directory, TranslationScripts.ExportFileName), TranslationScripts.ExportBat);

            Console.WriteLine();
            Console.WriteLine("다음 순서로 채우세요.");
            Console.WriteLine("  1. languages 에 쓰는 언어를, gameData.path 에 게임 DB 폴더를, targets 에 번역할 표와 컬럼을 적는다");
            Console.WriteLine("  2. sync.bat     게임 DB 원문으로 이 폴더에 LocalizationData.csv · TranslationData.csv 를 만든다");
            Console.WriteLine("  3. 번역가가 CSV 의 언어 열을 채운다");
            Console.WriteLine("  4. export.bat   Bundles/ 에 언어별 json + manifest.json + LocalKey.csv 를 굽는다");
            return ExitOk;
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (File.Exists(path))
                return;

            File.WriteAllText(path, content, new UTF8Encoding(false));
            Console.WriteLine("만들었습니다: " + Path.GetFileName(path));
        }

        private static int Export(CliOptions options)
        {
            var document = LoadConfig(options);
            var source = AdapterFactory.CreateTranslationSource(document, out var sourceLabel, out var owned);

            using (owned)
            {
                var bundleOptions = AdapterFactory.CreateBundleOptions(document, options.Version, sourceLabel);
                var directory = AdapterFactory.OutputDirectory(document, options.OutputPath);

                Console.WriteLine("번역 시트  " + sourceLabel);
                Console.WriteLine("출력       " + directory);
                Console.WriteLine("(시트를 읽기만 합니다. 게임 DB에 접근하지 않습니다)");
                Console.WriteLine();

                var result = new TranslationExporter(document.Config).Export(source, bundleOptions);

                PrintValidation(result.Validation, options);

                if (options.Strict && result.Validation.HasErrors)
                {
                    PrintWarnings(result.Warnings, options);
                    Console.Error.WriteLine("검증 오류가 있어 익스포트를 중단했습니다 (--strict).");
                    return ExitValidation;
                }

                BundleBuilder.WriteToDirectory(result.Bundle, directory);

                if (AdapterFactory.ScriptOutput(document, out var scriptDirectory, out var ns))
                {
                    var written = LocalKeyScript.WriteFiles(
                        result.Entries, document.Config.Languages.SourceLanguageId, ns, scriptDirectory, result.Warnings);
                    foreach (var path in written)
                        Console.WriteLine("코드       " + path);
                }

                PrintWarnings(result.Warnings, options);

                Console.WriteLine();
                Console.WriteLine("버전 " + result.Bundle.Manifest.BundleVersion);
                foreach (var language in result.Bundle.Manifest.Languages)
                {
                    var bytes = result.Bundle.Files[language.File].Length;
                    Console.WriteLine(
                        "  " + Pad(language.Id, 10) + Pad(language.EntryCount + "개", 12)
                        + Pad(Size(bytes), 10) + language.Hash.Substring(0, 12));
                }

                Console.WriteLine();
                Console.WriteLine("시트 " + result.SheetCount + "장, 엔트리 " + result.Entries.Count
                                  + "개, 총 " + Size(result.Bundle.TotalBytes));
                return ExitOk;
            }
        }

        private static int Validate(CliOptions options)
        {
            var document = LoadConfig(options);
            var source = AdapterFactory.CreateTranslationSource(document, out var sourceLabel, out var owned);

            using (owned)
            {
                Console.WriteLine("번역 시트  " + sourceLabel);
                Console.WriteLine();

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

                var validator = new TranslationValidator(document.Config) { TreatMissingAsError = options.Strict };
                var sync = AdapterFactory.CreateSyncOptions(document, false, false);
                validator.UnfinishedMarkers.Clear();
                validator.UnfinishedMarkers.Add(sync.ChangedMarker);
                validator.UnfinishedMarkers.Add(sync.SuggestedMarker);

                var report = validator.Validate(entries);

                PrintValidation(report, options);
                PrintWarnings(warnings, options);

                return report.HasErrors ? ExitValidation : ExitOk;
            }
        }

        private static int Sync(CliOptions options)
        {
            var document = LoadConfig(options);
            var syncOptions = AdapterFactory.CreateSyncOptions(document, options.Prune, options.DryRun);
            var gameData = AdapterFactory.CreateGameDataSource(document, out var ownedGameData);

            using (ownedGameData)
            {
                var source = AdapterFactory.CreateTranslationSource(
                    document, out var sourceLabel, out var owned, writable: !options.DryRun);

                using (owned)
                {
                    if (!(source is ITranslationSheetStore store))
                        throw new TranslationConfigException("이 번역 시트 종류는 sync 를 지원하지 않습니다: " + sourceLabel);

                    Console.WriteLine("게임 DB    " + gameData.Label);
                    Console.WriteLine("번역 시트  " + sourceLabel);
                    if (options.DryRun)
                        Console.WriteLine("(--dry-run: 결과만 보여 주고 시트에 쓰지 않습니다)");
                    else if (options.Prune)
                        Console.WriteLine("(--prune: 게임 DB 에서 사라진 키의 행을 지웁니다)");
                    else
                        Console.WriteLine("(게임 DB 에서 사라진 키는 지우지 않고 " + syncOptions.StateColumn + " 에 "
                                          + syncOptions.StateRemoved + " 으로 표시합니다. 지우려면 --prune)");

                    Console.WriteLine();

                    var result = new TranslationSynchronizer(document.Config).Sync(gameData, store, syncOptions);

                    foreach (var sheet in result.Sheets)
                    {
                        Console.WriteLine("  " + sheet + (sheet.Written ? "  (씀)" : sheet.Skipped ? string.Empty : "  (변경 없음)"));
                        foreach (var target in sheet.SkippedTargets)
                            Console.WriteLine("      건너뜀: " + target);
                    }

                    PrintWarnings(result.Warnings, options);

                    Console.WriteLine();
                    Console.WriteLine("신규 " + result.Added + "개 · 원문변경 " + result.Changed + "개 · 추천 "
                                      + result.Suggested + "개 · 삭제됨 " + result.Removed + "개 · 지움 "
                                      + result.Pruned + "개 · 그대로 " + result.Unchanged + "개");

                    if (result.Changed > 0 || result.Suggested > 0)
                        Console.WriteLine("번역 앞에 붙은 " + syncOptions.ChangedMarker + " · " + syncOptions.SuggestedMarker
                                          + " 은 번역가가 확인한 뒤 지웁니다. validate 가 남은 것을 잡아 줍니다.");

                    return ExitOk;
                }
            }
        }

        private static int Diff(CliOptions options)
        {
            if (options.Positional.Count < 2)
            {
                Console.Error.WriteLine("diff는 매니페스트 두 개가 필요합니다: translation diff <이전> <이후>");
                return ExitUsage;
            }

            var local = File.Exists(options.Positional[0])
                ? BundleReader.ReadManifest(File.ReadAllBytes(options.Positional[0]))
                : null;

            var remote = BundleReader.ReadManifest(File.ReadAllBytes(options.Positional[1]));
            var diff = ManifestDiff.Compare(local, remote);

            Console.WriteLine("이전 " + (local?.BundleVersion ?? "(없음)"));
            Console.WriteLine("이후 " + remote.BundleVersion);
            Console.WriteLine();
            Console.WriteLine("변경   " + Join(diff.ChangedLanguages));
            Console.WriteLine("추가   " + Join(diff.AddedLanguages));
            Console.WriteLine("삭제   " + Join(diff.RemovedLanguages));
            Console.WriteLine("동일   " + Join(diff.UnchangedLanguages));
            Console.WriteLine();
            Console.WriteLine("내려받을 언어: " + Join(diff.LanguagesToFetch));

            return ExitOk;
        }

        private static TranslationConfigDocument LoadConfig(CliOptions options)
        {
            return TranslationConfigFile.Load(options.ConfigPath ?? TranslationConfigFile.DefaultFileName);
        }

        private static void PrintValidation(ValidationReport report, CliOptions options)
        {
            Console.WriteLine("검증 " + report);

            foreach (var pair in report.MissingByLanguage)
            {
                if (pair.Value > 0)
                    Console.WriteLine("  미번역 " + Pad(pair.Key, 10) + pair.Value + "개");
            }

            var shown = 0;
            var limit = options.Verbose ? int.MaxValue : 20;
            foreach (var issue in report.Issues)
            {
                if (issue.Severity == IssueSeverity.Info)
                    continue;

                if (shown++ >= limit)
                {
                    Console.WriteLine("  ... 나머지는 --verbose로 확인하세요.");
                    break;
                }

                Console.WriteLine("  " + issue);
            }
        }

        private static void PrintWarnings(IReadOnlyList<string> warnings, CliOptions options)
        {
            if (warnings == null || warnings.Count == 0)
                return;

            var limit = options.Verbose ? warnings.Count : Math.Min(10, warnings.Count);
            Console.WriteLine();
            Console.WriteLine("경고 " + warnings.Count + "건");
            for (var i = 0; i < limit; i++)
                Console.WriteLine("  " + warnings[i]);

            if (limit < warnings.Count)
                Console.WriteLine("  ... 나머지는 --verbose로 확인하세요.");
        }

        private static string Join(IReadOnlyList<string> values)
        {
            return values == null || values.Count == 0 ? "(없음)" : string.Join(", ", values);
        }

        private static string Pad(string value, int width)
        {
            return value.Length >= width ? value + " " : value.PadRight(width);
        }

        private static string Size(int bytes)
        {
            if (bytes < 1024)
                return bytes + "B";

            if (bytes < 1024 * 1024)
                return (bytes / 1024.0).ToString("0.0") + "KB";

            return (bytes / (1024.0 * 1024.0)).ToString("0.00") + "MB";
        }

        private static int Usage()
        {
            Console.WriteLine(Templates.Usage);
            return ExitUsage;
        }
    }
}
