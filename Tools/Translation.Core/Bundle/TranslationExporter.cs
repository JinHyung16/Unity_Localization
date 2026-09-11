using System;
using System.Collections.Generic;

namespace Translation
{
    public sealed class ExportResult
    {
        public BundleBuildResult Bundle { get; set; }

        public ValidationReport Validation { get; set; }

        public List<TranslateEntry> Entries { get; } = new List<TranslateEntry>();

        public List<string> Warnings { get; } = new List<string>();

        public int SheetCount { get; set; }
    }

    /// <summary> 번역 시트를 읽어 게임에 넣을 번들을 만든다. 게임 DB 에 접근하지 않는다 </summary>
    public sealed class TranslationExporter
    {
        private readonly TranslationConfig _config;

        public TranslationExporter(TranslationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ExportResult Export(ITranslationSource source, BundleBuildOptions options = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new ExportResult();

            // 시트 단위로 돈다 — 대상 여럿이 한 시트를 공유할 수 있다
            foreach (var sheetName in _config.SheetNames)
            {
                var layout = _config.GetLayout(sheetName);
                if (!source.SheetExists(layout.SheetName))
                {
                    result.Warnings.Add("번역 시트가 없어 건너뜁니다: " + layout.SheetName);
                    continue;
                }

                var entries = source.Read(layout, result.Warnings);
                if (entries == null)
                    continue;

                result.SheetCount++;
                result.Entries.AddRange(entries);
            }

            result.Validation = new TranslationValidator(_config).Validate(result.Entries);
            result.Bundle = new BundleBuilder(_config).Build(result.Entries, options);
            result.Warnings.AddRange(result.Bundle.Warnings);

            return result;
        }
    }
}
