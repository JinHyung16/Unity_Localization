using System;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary> 런타임 조회 진입점. 번들 바이트를 받아 카탈로그를 세우고 언어 선택을 저장한다 </summary>
    public static class TranslationRuntime
    {
        public const string DefaultLanguagePrefKey = "Translation.Language";

        private static TranslationCatalog _catalog;
        private static bool _logMissingKeys;
        private static Action<Action<bool>> _reload;

        public static event Action<string> OnLanguageChanged;

        public static string LanguagePrefKey { get; set; } = DefaultLanguagePrefKey;

        public static TranslationCatalog Catalog
        {
            get { return _catalog; }
        }

        public static bool IsLoaded
        {
            get { return _catalog != null; }
        }

        public static string LanguageId
        {
            get { return _catalog?.LanguageId; }
        }

        public static BundleManifest Manifest
        {
            get { return _catalog?.Manifest; }
        }

        /// <summary>
        /// 지금 언어의 폰트 주소. 없으면 폴백 사슬을 타고, 끝까지 없으면 null.
        /// 주소로 폰트를 로드하는 것은 프로젝트가 한다
        /// </summary>
        public static string FontAddress
        {
            get
            {
                var manifest = Manifest;
                return manifest == null ? null : manifest.ResolveFontAddress(LanguageId);
            }
        }

        /// <summary> 번들 버전. 어느 패치가 적용됐는지 로그와 QA에 남길 때 쓴다 </summary>
        public static string BundleVersion
        {
            get { return _catalog?.Manifest?.BundleVersion; }
        }

        public static MissingKeyPolicy MissingKeyPolicy { get; set; } = MissingKeyPolicy.ReturnMarkedKey;

        /// <summary> 켜면 없는 키를 만날 때마다 경고 로그를 남긴다. QA 빌드에서 켜 둔다 </summary>
        public static bool LogMissingKeys
        {
            get { return _logMissingKeys; }
            set
            {
                if (_logMissingKeys == value)
                    return;

                _logMissingKeys = value;
                if (_catalog == null)
                    return;

                if (value)
                    _catalog.OnMissingKey += WarnMissing;
                else
                    _catalog.OnMissingKey -= WarnMissing;
            }
        }

        /// <summary> 언어 번들 바이트를 적재한다. 매니페스트는 없어도 동작하며 버전 보고용이다 </summary>
        public static void Load(byte[] languageBundleBytes, byte[] manifestBytes = null)
        {
            if (languageBundleBytes == null || languageBundleBytes.Length == 0)
                throw new TranslationBundleException("언어 번들 바이트가 비어 있습니다.");

            var catalog = TranslationCatalog.FromBytes(languageBundleBytes, manifestBytes);
            Apply(catalog);
        }

        public static void Apply(TranslationCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            if (_catalog != null)
                _catalog.OnMissingKey -= WarnMissing;

            catalog.MissingKeyPolicy = MissingKeyPolicy;
            if (_logMissingKeys)
                catalog.OnMissingKey += WarnMissing;

            _catalog = catalog;
            OnLanguageChanged?.Invoke(catalog.LanguageId);
        }

        public static void Unload()
        {
            if (_catalog != null)
                _catalog.OnMissingKey -= WarnMissing;

            _catalog = null;
            _reload = null;
        }

        /// <summary>
        /// 로더가 "같은 곳에서 다시 읽는 법"을 등록한다. SetLanguage 가 쓴다.
        /// 표준 로더는 알아서 등록하고, 바이트를 직접 Load 한 프로젝트만 필요하면 등록한다
        /// </summary>
        public static void RegisterReload(Action<Action<bool>> reload)
        {
            _reload = reload;
        }

        /// <summary>
        /// 언어를 저장하고 같은 곳에서 다시 읽는다. 끝나면 OnLanguageChanged 가 온다.
        /// 다시 읽는 법이 등록돼 있지 않으면 저장만 하고 done(false) 를 부른다
        /// </summary>
        public static void SetLanguage(string languageId, Action<bool> done = null)
        {
            SaveLanguage(languageId);

            if (_reload == null)
            {
                Debug.LogWarning("[Translation] 다시 읽는 법이 등록돼 있지 않아 언어를 저장만 했습니다. 로더를 다시 부르세요.");
                done?.Invoke(false);
                return;
            }

            _reload(ok => done?.Invoke(ok));
        }

        public static string Get(string key)
        {
            return _catalog == null ? Fallback(key) : _catalog.Get(key);
        }

        public static string Get(TranslateKey key)
        {
            return _catalog == null ? Fallback(BundleKey.Encode(key)) : _catalog.Get(key);
        }

        public static string Get(string table, string column, string id, string subId = null)
        {
            return Get(new TranslateKey(table, column, id, subId));
        }

        public static string Format(string key, params object[] args)
        {
            return _catalog == null ? Fallback(key) : _catalog.Format(key, args);
        }

        public static bool TryGet(string key, out string value)
        {
            if (_catalog != null)
                return _catalog.TryGet(key, out value);

            value = null;
            return false;
        }

        /// <summary> 저장된 언어 선택. 없으면 null </summary>
        public static string LoadSavedLanguage()
        {
            var saved = PlayerPrefs.GetString(LanguagePrefKey, string.Empty);
            return string.IsNullOrEmpty(saved) ? null : saved;
        }

        public static void SaveLanguage(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
                return;

            PlayerPrefs.SetString(LanguagePrefKey, languageId);
            PlayerPrefs.Save();
        }

        /// <summary> 쓸 언어를 정한다. 저장된 선택 → TranslateOption → 기기 언어 → 원문 언어 </summary>
        public static string ResolveLanguage(BundleManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            var saved = LoadSavedLanguage();
            if (saved != null && manifest.Find(saved) != null)
                return manifest.Find(saved).Id;

            var declared = TranslateOption.ResolveDisplayLanguage();
            if (declared != null && manifest.Find(declared) != null)
                return manifest.Find(declared).Id;

            var device = DeviceLanguage.Resolve(manifest);
            if (device != null)
                return device;

            if (manifest.Find(manifest.SourceLanguage) != null)
                return manifest.Find(manifest.SourceLanguage).Id;

            return manifest.Languages.Count > 0 ? manifest.Languages[0].Id : null;
        }

        private static string Fallback(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            switch (MissingKeyPolicy)
            {
                case MissingKeyPolicy.ReturnEmpty:
                    return string.Empty;
                case MissingKeyPolicy.ReturnKey:
                    return BundleKey.ToDisplay(key);
                default:
                    return TranslationCatalog.MissingKeyMarker + BundleKey.ToDisplay(key);
            }
        }

        private static void WarnMissing(string key)
        {
            Debug.LogWarning("[Translation] 번역 키가 번들에 없습니다: " + BundleKey.ToDisplay(key)
                             + " (언어 " + LanguageId + ", 번들 " + BundleVersion + ")");
        }
    }
}
