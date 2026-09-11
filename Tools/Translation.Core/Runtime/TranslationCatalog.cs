using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 키가 번들에 없을 때 무엇을 반환할지 </summary>
    public enum MissingKeyPolicy
    {
        /// <summary> 키 문자열 앞에 표시를 붙여 반환한다. 화면에서 눈에 띄므로 기본값이다 </summary>
        ReturnMarkedKey = 0,

        ReturnKey,

        ReturnEmpty,

        /// <summary> 예외를 던진다. 테스트와 CI에서 쓴다 </summary>
        Throw
    }

    /// <summary> 언어 번들 하나를 들고 키로 번역문을 찾는다 </summary>
    public sealed class TranslationCatalog
    {
        public const string MissingKeyMarker = "@";

        private readonly LanguageBundle _bundle;

        public string LanguageId { get; }

        /// <summary> 번들을 만든 매니페스트. 없이도 동작하며 버전 보고용이다 </summary>
        public BundleManifest Manifest { get; }

        public MissingKeyPolicy MissingKeyPolicy { get; set; } = MissingKeyPolicy.ReturnMarkedKey;

        /// <summary> 키를 찾지 못할 때마다 호출된다 </summary>
        public event Action<string> OnMissingKey;

        public TranslationCatalog(LanguageBundle bundle, BundleManifest manifest = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            LanguageId = bundle.LanguageId;
            Manifest = manifest;
        }

        public int Count
        {
            get { return _bundle.Count; }
        }

        public static TranslationCatalog FromBytes(byte[] languageBundleBytes, byte[] manifestBytes = null)
        {
            var bundle = BundleReader.ReadLanguage(languageBundleBytes);
            var manifest = manifestBytes == null ? null : BundleReader.ReadManifest(manifestBytes);
            return new TranslationCatalog(bundle, manifest);
        }

        public bool TryGet(string key, out string value)
        {
            return _bundle.TryGet(key, out value) && !string.IsNullOrEmpty(value);
        }

        public bool TryGet(TranslateKey key, out string value)
        {
            return TryGet(BundleKey.Encode(key), out value);
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (TryGet(key, out var value))
                return value;

            return Missing(key);
        }

        public string Get(TranslateKey key)
        {
            return Get(BundleKey.Encode(key));
        }

        public string Get(string table, string column, string id, string subId = null)
        {
            return Get(BundleKey.Encode(table, column, id, subId));
        }

        /// <summary> 조회한 뒤 string.Format을 적용한다. 포맷이 깨지면 원문을 그대로 반환한다 </summary>
        public string Format(string key, params object[] args)
        {
            var text = Get(key);
            if (args == null || args.Length == 0)
                return text;

            try
            {
                return string.Format(text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        public string Format(TranslateKey key, params object[] args)
        {
            return Format(BundleKey.Encode(key), args);
        }

        public IEnumerable<string> Keys
        {
            get { return _bundle.Entries.Keys; }
        }

        private string Missing(string key)
        {
            OnMissingKey?.Invoke(key);

            switch (MissingKeyPolicy)
            {
                case MissingKeyPolicy.ReturnEmpty:
                    return string.Empty;
                case MissingKeyPolicy.ReturnKey:
                    return BundleKey.ToDisplay(key);
                case MissingKeyPolicy.Throw:
                    throw new KeyNotFoundException(
                        "번역 키가 번들에 없습니다: " + BundleKey.ToDisplay(key) + " (언어 " + LanguageId + ")");
                default:
                    return MissingKeyMarker + BundleKey.ToDisplay(key);
            }
        }

        public override string ToString()
        {
            return "TranslationCatalog(" + LanguageId + ", " + Count + "개, v"
                   + (Manifest?.BundleVersion ?? "?") + ")";
        }
    }
}
