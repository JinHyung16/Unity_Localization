using System;
using System.Collections.Generic;

namespace Translation
{
    public enum IssueSeverity
    {
        Info = 0,
        Warning,
        Error
    }

    public sealed class ValidationIssue
    {
        public IssueSeverity Severity { get; set; }

        public string Code { get; set; }

        public string LanguageId { get; set; }

        public TranslateKey Key { get; set; }

        public string Message { get; set; }

        public override string ToString()
        {
            var prefix = Severity.ToString().ToUpperInvariant() + " " + Code;
            if (!string.IsNullOrEmpty(LanguageId))
                prefix += " [" + LanguageId + "]";

            return prefix + " " + Key + ": " + Message;
        }
    }

    public sealed class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        public Dictionary<string, int> MissingByLanguage { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int TotalEntries { get; set; }

        public int ErrorCount
        {
            get { return Count(IssueSeverity.Error); }
        }

        public int WarningCount
        {
            get { return Count(IssueSeverity.Warning); }
        }

        public bool HasErrors
        {
            get { return ErrorCount > 0; }
        }

        private int Count(IssueSeverity severity)
        {
            var total = 0;
            foreach (var issue in Issues)
            {
                if (issue.Severity == severity)
                    total++;
            }

            return total;
        }

        public override string ToString()
        {
            return "entries=" + TotalEntries + ", errors=" + ErrorCount + ", warnings=" + WarningCount;
        }
    }

    /// <summary> 읽어 들인 번역이 게임에 넣어도 안전한지 검사한다 </summary>
    public sealed class TranslationValidator
    {
        public const string CodeMissing = "missing-translation";
        public const string CodePlaceholder = "placeholder-mismatch";
        public const string CodeEmptySource = "empty-source";
        public const string CodeDuplicate = "duplicate-key";
        public const string CodeUnfinished = "unfinished-translation";

        private readonly TranslationConfig _config;

        /// <summary> 미번역을 Error로 볼지. 기본은 Warning이며 출시 전 CI에서만 올린다 </summary>
        public bool TreatMissingAsError { get; set; }

        /// <summary> sync 가 붙인 마커. 이걸로 시작하는 번역은 아직 끝나지 않은 것으로 본다 </summary>
        public List<string> UnfinishedMarkers { get; } = new List<string>
        {
            SyncOptions.DefaultChangedMarker,
            SyncOptions.DefaultSuggestedMarker,
        };

        /// <summary> 언어별로 리포트에 남길 미번역 샘플 개수 </summary>
        public int MissingSampleLimit { get; set; } = 20;

        public TranslationValidator(TranslationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ValidationReport Validate(IReadOnlyList<TranslateEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var report = new ValidationReport { TotalEntries = entries.Count };
            var sourceLang = _config.Languages.SourceLanguageId;
            var languageIds = _config.Languages.EnabledIds;
            var seen = new HashSet<TranslateKey>();
            var missingSamples = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var languageId in languageIds)
            {
                report.MissingByLanguage[languageId] = 0;
                missingSamples[languageId] = 0;
            }

            foreach (var entry in entries)
            {
                if (!seen.Add(entry.Key))
                {
                    Add(report, IssueSeverity.Error, CodeDuplicate, null, entry.Key, "키가 중복입니다.");
                    continue;
                }

                var source = entry.Get(sourceLang);
                if (string.IsNullOrEmpty(source))
                {
                    Add(report, IssueSeverity.Warning, CodeEmptySource, sourceLang, entry.Key,
                        "원문이 비어 있습니다.");
                    continue;
                }

                var sourcePlaceholders = PlaceholderSet.Extract(source);

                foreach (var languageId in languageIds)
                {
                    if (string.Equals(languageId, sourceLang, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var value = entry.Get(languageId);
                    if (string.IsNullOrEmpty(value))
                    {
                        report.MissingByLanguage[languageId] = report.MissingByLanguage[languageId] + 1;
                        if (missingSamples[languageId] < MissingSampleLimit)
                        {
                            missingSamples[languageId] = missingSamples[languageId] + 1;
                            Add(report,
                                TreatMissingAsError ? IssueSeverity.Error : IssueSeverity.Warning,
                                CodeMissing, languageId, entry.Key, "번역이 비어 있습니다.");
                        }

                        continue;
                    }

                    var marker = FindMarker(value);
                    if (marker != null)
                    {
                        Add(report,
                            TreatMissingAsError ? IssueSeverity.Error : IssueSeverity.Warning,
                            CodeUnfinished, languageId, entry.Key, "번역이 아직 끝나지 않았습니다: " + marker);
                        value = value.Substring(marker.Length);
                    }

                    var placeholders = PlaceholderSet.Extract(value);
                    if (PlaceholderSet.SameSet(sourcePlaceholders, placeholders))
                        continue;

                    Add(report, IssueSeverity.Error, CodePlaceholder, languageId, entry.Key,
                        "플레이스홀더가 원문과 다릅니다. 원문 " + PlaceholderSet.Describe(sourcePlaceholders)
                        + " / 번역 " + PlaceholderSet.Describe(placeholders)
                        + " — 런타임에 FormatException이 납니다.");
                }
            }

            return report;
        }

        private string FindMarker(string value)
        {
            foreach (var marker in UnfinishedMarkers)
            {
                if (!string.IsNullOrEmpty(marker) && value.StartsWith(marker, StringComparison.Ordinal))
                    return marker;
            }

            return null;
        }

        private static void Add(
            ValidationReport report,
            IssueSeverity severity,
            string code,
            string languageId,
            TranslateKey key,
            string message)
        {
            report.Issues.Add(new ValidationIssue
            {
                Severity = severity,
                Code = code,
                LanguageId = languageId,
                Key = key,
                Message = message,
            });
        }
    }
}
