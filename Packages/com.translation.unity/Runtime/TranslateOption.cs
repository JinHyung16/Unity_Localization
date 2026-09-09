using System;
using System.Collections.Generic;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary>
    /// 기기 언어와 시트 언어 열 이름을 짝짓는 Resources/TranslateOption.asset.
    /// 없어도 되고, 없으면 매니페스트의 bcp47 로 기기 언어를 맞춘다
    /// </summary>
    [CreateAssetMenu(menuName = "Translation/Translate Option", fileName = AssetName)]
    public class TranslateOption : ScriptableObject
    {
        public const string AssetName = "TranslateOption";

        /// <summary> 언어 하나. DisplayLanguage 가 시트의 언어 열 이름이자 번들 파일 이름이다 </summary>
        [Serializable]
        public struct Language : IEquatable<Language>
        {
            public SystemLanguage SystemLanguage;
            public string DisplayLanguage;
            public bool IsApply;

            public bool Equals(Language other)
            {
                return SystemLanguage == other.SystemLanguage;
            }

            public override bool Equals(object obj)
            {
                return obj is Language other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (int)SystemLanguage;
            }

            public override string ToString()
            {
                return SystemLanguage + "(" + DisplayLanguage + ")";
            }
        }

        [SerializeField] private Language[] _languages;
        [SerializeField] private int _defaultLanguageIndex;
        private Dictionary<SystemLanguage, Language> _bySystemLanguage;
        private Dictionary<string, Language> _byDisplayLanguage;
        private static TranslateOption _instance;
        private static bool _searched;

        /// <summary> Resources 에 없으면 null. 없는 것이 정상이다 </summary>
        public static TranslateOption GetInstance()
        {
            if (_searched == false)
            {
                _instance = Resources.Load<TranslateOption>(AssetName);
                _searched = true;
            }

            return _instance;
        }

        public static IReadOnlyList<Language> GetLanguages()
        {
            var instance = GetInstance();
            return instance == null ? Array.Empty<Language>() : instance._languages;
        }

        /// <summary> 기기 언어에 맞는 언어. 없으면 기본 언어 </summary>
        public static Language GetLanguage(SystemLanguage systemLanguage)
        {
            var instance = GetInstance();
            if (instance == null || instance.Initialize() == false)
                return default;

            if (instance._bySystemLanguage.TryGetValue(systemLanguage, out var language))
                return language;

            return instance.GetDefaultLanguage();
        }

        /// <summary> 시트 열 이름으로 찾는다. 없으면 기본 언어 </summary>
        public static Language GetLanguage(string displayLanguage)
        {
            var instance = GetInstance();
            if (instance == null || instance.Initialize() == false)
                return default;

            if (displayLanguage != null
                && instance._byDisplayLanguage.TryGetValue(displayLanguage, out var language))
                return language;

            return instance.GetDefaultLanguage();
        }

        /// <summary> 기기 언어가 가리키는 시트 열 이름. 선언이 없으면 null </summary>
        public static string ResolveDisplayLanguage()
        {
            return ResolveDisplayLanguage(Application.systemLanguage);
        }

        public static string ResolveDisplayLanguage(SystemLanguage systemLanguage)
        {
            var language = GetLanguage(systemLanguage);
            return string.IsNullOrEmpty(language.DisplayLanguage) ? null : language.DisplayLanguage;
        }

        /// <summary> 에디터가 목록을 고쳤을 때 다시 읽게 한다 </summary>
        public static void Invalidate()
        {
            _instance = null;
            _searched = false;
        }

        private bool Initialize()
        {
            if (_bySystemLanguage != null && _byDisplayLanguage != null)
                return true;

            if (_languages == null || _languages.Length == 0)
                return false;

            _bySystemLanguage = new Dictionary<SystemLanguage, Language>();
            _byDisplayLanguage = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < _languages.Length; i++)
            {
                var language = _languages[i];
                if (language.IsApply == false)
                    continue;

                _bySystemLanguage[language.SystemLanguage] = language;
                if (string.IsNullOrEmpty(language.DisplayLanguage) == false)
                    _byDisplayLanguage[language.DisplayLanguage] = language;
            }

            return true;
        }

        private Language GetDefaultLanguage()
        {
            if (_languages == null || _languages.Length == 0)
                return default;

            var index = _defaultLanguageIndex;
            if (index < 0 || index >= _languages.Length)
                index = 0;

            return _languages[index];
        }
    }
}
