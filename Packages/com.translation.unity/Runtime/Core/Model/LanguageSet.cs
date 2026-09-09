using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 프로젝트가 선언한 언어 목록과 폴백 체인. 언어 식별자 체계를 SDK가 강제하지 않는다 </summary>
    public sealed class LanguageSet
    {
        private readonly List<LanguageDefinition> _languages = new List<LanguageDefinition>();
        private readonly Dictionary<string, LanguageDefinition> _byId =
            new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 원문 언어 Id. 게임 DB에서 읽어오는 텍스트가 이 언어이고, 최종 폴백이 된다 </summary>
        public string SourceLanguageId { get; private set; }

        public LanguageSet(IEnumerable<LanguageDefinition> languages, string sourceLanguageId)
        {
            if (languages == null)
                throw new ArgumentNullException(nameof(languages));

            foreach (var lang in languages)
            {
                lang.Validate();
                if (_byId.ContainsKey(lang.Id))
                    throw new TranslationConfigException("언어 Id가 중복입니다: " + lang.Id);

                _languages.Add(lang);
                _byId.Add(lang.Id, lang);
            }

            if (_languages.Count == 0)
                throw new TranslationConfigException("언어가 하나도 선언되지 않았습니다.");

            if (string.IsNullOrWhiteSpace(sourceLanguageId))
                throw new TranslationConfigException("sourceLanguageId는 필수입니다.");

            if (!_byId.TryGetValue(sourceLanguageId, out var source))
                throw new TranslationConfigException("sourceLanguageId가 선언된 언어 목록에 없습니다: " + sourceLanguageId);

            SourceLanguageId = source.Id;
            ValidateFallbacks();
        }

        /// <summary> 선언 순서대로의 전체 언어 목록 (Enabled=false 포함) </summary>
        public IReadOnlyList<LanguageDefinition> All
        {
            get { return _languages; }
        }

        /// <summary> Enabled=true인 언어 Id 목록. 동기화·익스포트 대상 </summary>
        public IReadOnlyList<string> EnabledIds
        {
            get
            {
                var ids = new List<string>(_languages.Count);
                foreach (var lang in _languages)
                {
                    if (lang.Enabled)
                        ids.Add(lang.Id);
                }
                return ids;
            }
        }

        public bool Contains(string languageId)
        {
            return !string.IsNullOrEmpty(languageId) && _byId.ContainsKey(languageId);
        }

        /// <summary> 선언된 Id를 정규 표기(선언 당시 대소문자)로 되돌린다. 미선언이면 null </summary>
        public string Normalize(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
                return null;

            return _byId.TryGetValue(languageId, out var lang) ? lang.Id : null;
        }

        public LanguageDefinition Get(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
                return null;

            return _byId.TryGetValue(languageId, out var lang) ? lang : null;
        }

        /// <summary> languageId부터 시작해 폴백을 따라간 조회 순서. 마지막은 항상 원문 언어다 </summary>
        public IReadOnlyList<string> GetFallbackChain(string languageId)
        {
            var chain = new List<string>(4);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var current = Normalize(languageId);
            while (current != null && seen.Add(current))
            {
                chain.Add(current);
                var def = _byId[current];
                current = string.IsNullOrEmpty(def.FallbackId) ? null : Normalize(def.FallbackId);
            }

            if (seen.Add(SourceLanguageId))
                chain.Add(SourceLanguageId);

            return chain;
        }

        /// <summary> BCP-47 태그로 언어를 찾는다. 정확히 일치하는 것이 없으면 앞부분(예: en-GB → en)으로 재시도 </summary>
        public LanguageDefinition FindByBcp47(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            foreach (var lang in _languages)
            {
                if (string.Equals(lang.Bcp47, tag, StringComparison.OrdinalIgnoreCase))
                    return lang;
            }

            var dash = tag.IndexOf('-');
            if (dash <= 0)
                return null;

            var prefix = tag.Substring(0, dash);
            foreach (var lang in _languages)
            {
                if (string.Equals(lang.Bcp47, prefix, StringComparison.OrdinalIgnoreCase))
                    return lang;
            }

            return null;
        }

        private void ValidateFallbacks()
        {
            foreach (var lang in _languages)
            {
                if (string.IsNullOrEmpty(lang.FallbackId))
                    continue;

                if (!_byId.ContainsKey(lang.FallbackId))
                    throw new TranslationConfigException(
                        "언어 " + lang.Id + "의 fallbackId가 선언 목록에 없습니다: " + lang.FallbackId);
            }

            foreach (var lang in _languages)
            {
                var chain = GetFallbackChain(lang.Id);
                if (chain[chain.Count - 1] != SourceLanguageId)
                    throw new TranslationConfigException(
                        "언어 " + lang.Id + "의 폴백 체인이 원문 언어에 도달하지 않습니다.");
            }
        }
    }
}
