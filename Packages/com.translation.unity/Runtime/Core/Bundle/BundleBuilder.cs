using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Translation
{
    public enum BundleCompression
    {
        None = 0,
        Gzip
    }

    public sealed class BundleBuildOptions
    {
        /// <summary> 패치 버전 문자열. 비우면 UTC 타임스탬프를 쓴다 </summary>
        public string BundleVersion { get; set; }

        /// <summary> 무엇으로부터 뽑았는지 남기는 감사용 라벨 </summary>
        public string SourceLabel { get; set; }

        /// <summary> true 면 빈 번역을 폴백으로 채워 언어 파일 하나만으로 완전하게 만든다 </summary>
        public bool BakeFallback { get; set; } = true;

        public BundleCompression Compression { get; set; } = BundleCompression.None;

        public bool IndentJson { get; set; }

        /// <summary> 언어 파일 확장자. 비우면 .trans / .trans.gz. Unity 가 읽으려면 ".json" </summary>
        public string FileExtension { get; set; }

        /// <summary> 매니페스트 파일 이름. 비우면 manifest.json </summary>
        public string ManifestFileName { get; set; }

        /// <summary> 이름을 주면 전 언어를 한 장에 담은 CSV 를 같이 굽는다. 비우면 안 만든다 </summary>
        public string CsvFileName { get; set; }
    }

    public sealed class BundleBuildResult
    {
        public BundleManifest Manifest { get; set; }

        /// <summary> 파일 이름 → 내용. manifest.json도 포함되어 있다 </summary>
        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public List<string> Warnings { get; } = new List<string>();

        public int TotalBytes
        {
            get
            {
                var total = 0;
                foreach (var pair in Files)
                    total += pair.Value.Length;

                return total;
            }
        }
    }

    /// <summary> 번역 엔트리를 언어별 번들 파일과 매니페스트로 굽는다 </summary>
    public sealed class BundleBuilder
    {
        private readonly TranslationConfig _config;

        public BundleBuilder(TranslationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public BundleBuildResult Build(IReadOnlyList<TranslateEntry> entries, BundleBuildOptions options = null)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            options = options ?? new BundleBuildOptions();
            var result = new BundleBuildResult();
            var languages = _config.Languages;
            var tables = new SortedSet<string>(StringComparer.Ordinal);

            var manifest = new BundleManifest
            {
                BundleVersion = string.IsNullOrEmpty(options.BundleVersion)
                    ? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                    : options.BundleVersion,
                CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                SourceLabel = options.SourceLabel,
                SourceLanguage = languages.SourceLanguageId,
                FallbackBaked = options.BakeFallback,
            };

            foreach (var languageId in languages.EnabledIds)
            {
                var bundle = new LanguageBundle(languageId, entries.Count);

                foreach (var entry in entries)
                {
                    if (entry.Key.IsEmpty)
                        continue;

                    var value = options.BakeFallback
                        ? entry.Resolve(languages, languageId)
                        : entry.Get(languageId);

                    if (string.IsNullOrEmpty(value))
                        continue;

                    bundle.Set(BundleKey.Encode(entry.Key), value);

                    if (!entry.Key.IsStandalone)
                        tables.Add(entry.Key.Table);
                }

                var json = bundle.ToJson(options.IndentJson);
                var bytes = Encode(json, options.Compression);
                var fileName = languageId + ResolveExtension(options);

                result.Files[fileName] = bytes;

                var def = languages.Get(languageId);
                manifest.Languages.Add(new BundleLanguageInfo
                {
                    Id = languageId,
                    DisplayName = def?.ResolvedDisplayName,
                    Bcp47 = def?.Bcp47,
                    FallbackId = def?.FallbackId,
                    FontAddress = def?.FontAddress,
                    File = fileName,
                    EntryCount = bundle.Count,
                    Hash = Sha256Hex(bytes),
                });

                if (bundle.Count == 0)
                    result.Warnings.Add("언어 " + languageId + "의 번들이 비어 있습니다.");
            }

            foreach (var table in tables)
                manifest.Tables.Add(table);

            if (string.IsNullOrEmpty(options.CsvFileName) == false)
            {
                var csv = LocalKeyCsv.Write(entries, languages, languages.EnabledIds,
                    options.BakeFallback);
                result.Files[options.CsvFileName] = Encoding.UTF8.GetBytes(csv);
            }

            result.Manifest = manifest;
            result.Files[ResolveManifestName(options)] =
                Encoding.UTF8.GetBytes(manifest.ToJson(true));

            return result;
        }

        public static void WriteToDirectory(BundleBuildResult result, string directory)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory는 필수입니다.", nameof(directory));

            Directory.CreateDirectory(directory);
            foreach (var pair in result.Files)
                File.WriteAllBytes(Path.Combine(directory, pair.Key), pair.Value);
        }

        /// <summary> 옵션이 지정한 확장자. 없으면 압축에 따른 기본값 </summary>
        public static string ResolveExtension(BundleBuildOptions options)
        {
            if (options != null && !string.IsNullOrEmpty(options.FileExtension))
                return options.FileExtension;

            return FileExtension(options == null ? BundleCompression.None : options.Compression);
        }

        public static string ResolveManifestName(BundleBuildOptions options)
        {
            return options != null && !string.IsNullOrEmpty(options.ManifestFileName)
                ? options.ManifestFileName
                : BundleManifest.FileName;
        }

        public static string FileExtension(BundleCompression compression)
        {
            return compression == BundleCompression.Gzip ? ".trans.gz" : ".trans";
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));

                return sb.ToString();
            }
        }

        private static byte[] Encode(string json, BundleCompression compression)
        {
            var raw = Encoding.UTF8.GetBytes(json);
            if (compression == BundleCompression.None)
                return raw;

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    gzip.Write(raw, 0, raw.Length);

                return output.ToArray();
            }
        }
    }
}
