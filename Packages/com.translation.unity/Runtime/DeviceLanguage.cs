using System;
using System.Collections.Generic;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary> 기기 언어를 매니페스트의 bcp47 로 맞춰 언어 식별자를 찾는다 </summary>
    public static class DeviceLanguage
    {
        /// <summary> 기기 언어에 맞는 언어 식별자. 못 맞추면 null </summary>
        public static string Resolve(BundleManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            return Resolve(manifest, Application.systemLanguage);
        }

        public static string Resolve(BundleManifest manifest, SystemLanguage systemLanguage)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            var tag = ToBcp47(systemLanguage);

            if (tag != null)
            {
                var byTag = FindByBcp47(manifest, tag);
                if (byTag != null)
                    return byTag;

                var dash = tag.IndexOf('-');
                if (dash > 0)
                {
                    var byPrefix = FindByBcp47(manifest, tag.Substring(0, dash));
                    if (byPrefix != null)
                        return byPrefix;
                }
            }

            var byName = systemLanguage.ToString();
            foreach (var language in manifest.Languages)
            {
                if (string.Equals(language.Id, byName, StringComparison.OrdinalIgnoreCase))
                    return language.Id;
            }

            return null;
        }

        /// <summary> Unity SystemLanguage를 BCP-47 태그로 옮긴다. 모르는 값은 null </summary>
        public static string ToBcp47(SystemLanguage language)
        {
            return Map.TryGetValue(language, out var tag) ? tag : null;
        }

        private static string FindByBcp47(BundleManifest manifest, string tag)
        {
            foreach (var language in manifest.Languages)
            {
                if (!string.IsNullOrEmpty(language.Bcp47)
                    && string.Equals(language.Bcp47, tag, StringComparison.OrdinalIgnoreCase))
                    return language.Id;
            }

            return null;
        }

        private static readonly Dictionary<SystemLanguage, string> Map = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.Afrikaans, "af" },
            { SystemLanguage.Arabic, "ar" },
            { SystemLanguage.Basque, "eu" },
            { SystemLanguage.Belarusian, "be" },
            { SystemLanguage.Bulgarian, "bg" },
            { SystemLanguage.Catalan, "ca" },
            { SystemLanguage.ChineseSimplified, "zh-Hans" },
            { SystemLanguage.ChineseTraditional, "zh-Hant" },
            { SystemLanguage.Czech, "cs" },
            { SystemLanguage.Danish, "da" },
            { SystemLanguage.Dutch, "nl" },
            { SystemLanguage.English, "en" },
            { SystemLanguage.Estonian, "et" },
            { SystemLanguage.Faroese, "fo" },
            { SystemLanguage.Finnish, "fi" },
            { SystemLanguage.French, "fr" },
            { SystemLanguage.German, "de" },
            { SystemLanguage.Greek, "el" },
            { SystemLanguage.Hebrew, "he" },
            { SystemLanguage.Hungarian, "hu" },
            { SystemLanguage.Icelandic, "is" },
            { SystemLanguage.Indonesian, "id" },
            { SystemLanguage.Italian, "it" },
            { SystemLanguage.Japanese, "ja" },
            { SystemLanguage.Korean, "ko" },
            { SystemLanguage.Latvian, "lv" },
            { SystemLanguage.Lithuanian, "lt" },
            { SystemLanguage.Norwegian, "no" },
            { SystemLanguage.Polish, "pl" },
            { SystemLanguage.Portuguese, "pt" },
            { SystemLanguage.Romanian, "ro" },
            { SystemLanguage.Russian, "ru" },
            { SystemLanguage.SerboCroatian, "sh" },
            { SystemLanguage.Slovak, "sk" },
            { SystemLanguage.Slovenian, "sl" },
            { SystemLanguage.Spanish, "es" },
            { SystemLanguage.Swedish, "sv" },
            { SystemLanguage.Thai, "th" },
            { SystemLanguage.Turkish, "tr" },
            { SystemLanguage.Ukrainian, "uk" },
            { SystemLanguage.Vietnamese, "vi" },
            { SystemLanguage.Hindi, "hi" },
        };
    }
}
