using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 번역 시트의 한 행. 키 하나에 대한 전 언어 값 </summary>
    public sealed class TranslateEntry
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TranslateKey Key { get; set; }

        public IReadOnlyDictionary<string, string> Values
        {
            get { return _values; }
        }

        public string Get(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
                return string.Empty;

            return _values.TryGetValue(languageId, out var value) ? value : string.Empty;
        }

        public bool Has(string languageId)
        {
            return !string.IsNullOrEmpty(Get(languageId));
        }

        public void Set(string languageId, string value)
        {
            if (string.IsNullOrEmpty(languageId))
                throw new ArgumentException("languageId는 필수입니다.", nameof(languageId));

            _values[languageId] = value ?? string.Empty;
        }

        /// <summary> languageId부터 폴백 체인을 따라 처음 발견된 비지 않은 값을 반환한다 </summary>
        public string Resolve(LanguageSet languages, string languageId)
        {
            if (languages == null)
                throw new ArgumentNullException(nameof(languages));

            var chain = languages.GetFallbackChain(languageId);
            for (var i = 0; i < chain.Count; i++)
            {
                var value = Get(chain[i]);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return string.Empty;
        }

        public override string ToString()
        {
            return Key.ToString();
        }
    }
}
