using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 한 언어의 번역 전체. 이 파일 하나만 있으면 그 언어로 동작한다 </summary>
    public sealed class LanguageBundle
    {
        public const int CurrentFormatVersion = 1;

        private readonly Dictionary<string, string> _entries;

        public int FormatVersion { get; set; } = CurrentFormatVersion;

        public string LanguageId { get; set; }

        public IReadOnlyDictionary<string, string> Entries
        {
            get { return _entries; }
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public LanguageBundle(string languageId, int capacity = 0)
        {
            LanguageId = languageId;
            _entries = new Dictionary<string, string>(capacity, StringComparer.Ordinal);
        }

        public void Set(string bundleKey, string value)
        {
            if (string.IsNullOrEmpty(bundleKey))
                return;

            _entries[bundleKey] = value ?? string.Empty;
        }

        public bool TryGet(string bundleKey, out string value)
        {
            if (string.IsNullOrEmpty(bundleKey))
            {
                value = null;
                return false;
            }

            return _entries.TryGetValue(bundleKey, out value);
        }

        public string ToJson(bool indent = false)
        {
            var entries = new Dictionary<string, object>(_entries.Count);
            foreach (var pair in _entries)
                entries[pair.Key] = pair.Value;

            var root = new Dictionary<string, object>
            {
                { "formatVersion", FormatVersion },
                { "language", LanguageId },
                { "entries", entries },
            };

            return MiniJson.Write(root, indent);
        }

        public static LanguageBundle FromJson(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.ParseObject(json);
            }
            catch (Exception e)
            {
                throw new TranslationBundleException("언어 번들을 읽을 수 없습니다: " + e.Message, e);
            }

            var formatVersion = MiniJson.GetInt(root, "formatVersion", 0);
            if (formatVersion > CurrentFormatVersion)
                throw new TranslationBundleException(
                    "지원하지 않는 언어 번들 포맷 버전입니다: " + formatVersion
                    + " (이 SDK는 " + CurrentFormatVersion + "까지)");

            var entries = MiniJson.GetObject(root, "entries");
            var bundle = new LanguageBundle(MiniJson.GetString(root, "language"), entries?.Count ?? 0)
            {
                FormatVersion = formatVersion,
            };

            if (entries != null)
            {
                foreach (var pair in entries)
                    bundle.Set(pair.Key, pair.Value as string ?? string.Empty);
            }

            return bundle;
        }

        public override string ToString()
        {
            return (LanguageId ?? "?") + " (" + Count + "개)";
        }
    }
}
