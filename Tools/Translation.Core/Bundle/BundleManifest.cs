using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 매니페스트에 기록되는 언어 하나의 정보 </summary>
    public sealed class BundleLanguageInfo
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Bcp47 { get; set; }

        public string FallbackId { get; set; }

        /// <summary> 이 언어의 폰트 주소. 비어 있으면 FallbackId 사슬을 탄다 </summary>
        public string FontAddress { get; set; }

        public string File { get; set; }

        public int EntryCount { get; set; }

        /// <summary> 파일 내용의 SHA-256 16진 문자열. 패치 필요 판정에 쓴다 </summary>
        public string Hash { get; set; }

        public override string ToString()
        {
            return Id + " (" + EntryCount + "개)";
        }
    }

    /// <summary> 번들 하나의 목차. 이것만 비교하면 어느 언어 파일이 바뀌었는지 안다 </summary>
    public sealed class BundleManifest
    {
        public const int CurrentFormatVersion = 1;
        public const string FileName = "manifest.json";

        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary> 패치 버전 문자열. 익스포트할 때 지정한다 </summary>
        public string BundleVersion { get; set; }

        public string CreatedUtc { get; set; }

        /// <summary> 무엇으로부터 뽑았는지 남기는 감사용 라벨 (예: sheet:13Cxr4..., excel:D:\gamedata) </summary>
        public string SourceLabel { get; set; }

        public string SourceLanguage { get; set; }

        /// <summary> 폴백이 번들에 이미 반영됐는지. true면 각 언어 파일이 자기 완결적이다 </summary>
        public bool FallbackBaked { get; set; }

        public List<BundleLanguageInfo> Languages { get; set; } = new List<BundleLanguageInfo>();

        /// <summary> 번들에 포함된 테이블 목록. 진단용이며 런타임 동작에는 쓰이지 않는다 </summary>
        public List<string> Tables { get; set; } = new List<string>();

        /// <summary> 그 언어의 폰트 주소. 없으면 폴백 사슬을 타고, 끝까지 없으면 null </summary>
        public string ResolveFontAddress(string languageId)
        {
            var seen = 0;
            var current = languageId;
            while (!string.IsNullOrEmpty(current) && seen++ < 32)
            {
                var lang = Find(current);
                if (lang == null)
                    return null;

                if (!string.IsNullOrEmpty(lang.FontAddress))
                    return lang.FontAddress;

                current = string.IsNullOrEmpty(lang.FallbackId) ? SourceLanguage : lang.FallbackId;
                if (string.Equals(current, lang.Id, StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            return null;
        }

        public BundleLanguageInfo Find(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
                return null;

            foreach (var lang in Languages)
            {
                if (string.Equals(lang.Id, languageId, StringComparison.OrdinalIgnoreCase))
                    return lang;
            }

            return null;
        }

        public string ToJson(bool indent = true)
        {
            var languages = new List<object>(Languages.Count);
            foreach (var lang in Languages)
            {
                languages.Add(new Dictionary<string, object>
                {
                    { "id", lang.Id },
                    { "displayName", lang.DisplayName },
                    { "bcp47", lang.Bcp47 },
                    { "fallbackId", lang.FallbackId },
                    { "fontAddress", lang.FontAddress },
                    { "file", lang.File },
                    { "entryCount", lang.EntryCount },
                    { "hash", lang.Hash },
                });
            }

            var tables = new List<object>(Tables.Count);
            foreach (var table in Tables)
                tables.Add(table);

            var root = new Dictionary<string, object>
            {
                { "formatVersion", FormatVersion },
                { "bundleVersion", BundleVersion },
                { "createdUtc", CreatedUtc },
                { "sourceLabel", SourceLabel },
                { "sourceLanguage", SourceLanguage },
                { "fallbackBaked", FallbackBaked },
                { "languages", languages },
                { "tables", tables },
            };

            return MiniJson.Write(root, indent);
        }

        public static BundleManifest FromJson(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.ParseObject(json);
            }
            catch (Exception e)
            {
                throw new TranslationBundleException("매니페스트를 읽을 수 없습니다: " + e.Message, e);
            }

            var manifest = new BundleManifest
            {
                FormatVersion = MiniJson.GetInt(root, "formatVersion", 0),
                BundleVersion = MiniJson.GetString(root, "bundleVersion"),
                CreatedUtc = MiniJson.GetString(root, "createdUtc"),
                SourceLabel = MiniJson.GetString(root, "sourceLabel"),
                SourceLanguage = MiniJson.GetString(root, "sourceLanguage"),
                FallbackBaked = MiniJson.GetBool(root, "fallbackBaked"),
            };

            if (manifest.FormatVersion > CurrentFormatVersion)
                throw new TranslationBundleException(
                    "지원하지 않는 매니페스트 포맷 버전입니다: " + manifest.FormatVersion
                    + " (이 SDK는 " + CurrentFormatVersion + "까지)");

            var languages = MiniJson.GetArray(root, "languages");
            if (languages != null)
            {
                foreach (var item in languages)
                {
                    if (!(item is Dictionary<string, object> map))
                        continue;

                    manifest.Languages.Add(new BundleLanguageInfo
                    {
                        Id = MiniJson.GetString(map, "id"),
                        DisplayName = MiniJson.GetString(map, "displayName"),
                        Bcp47 = MiniJson.GetString(map, "bcp47"),
                        FallbackId = MiniJson.GetString(map, "fallbackId"),
                        FontAddress = MiniJson.GetString(map, "fontAddress"),
                        File = MiniJson.GetString(map, "file"),
                        EntryCount = MiniJson.GetInt(map, "entryCount"),
                        Hash = MiniJson.GetString(map, "hash"),
                    });
                }
            }

            var tables = MiniJson.GetArray(root, "tables");
            if (tables != null)
            {
                foreach (var item in tables)
                {
                    if (item is string table)
                        manifest.Tables.Add(table);
                }
            }

            return manifest;
        }
    }
}
