using System;

namespace Translation
{
    /// <summary> 프로젝트가 선언하는 언어 하나. Id가 시트 컬럼명이자 번들 키가 된다 </summary>
    public sealed class LanguageDefinition
    {
        /// <summary> 프로젝트 고유 식별자. 시트 컬럼명·번들 파일명으로 그대로 쓰인다 (예: KR, US, TW, Korean, English) </summary>
        public string Id { get; set; }

        /// <summary> UI에 노출할 이름 (예: 한국어, English). 비어 있으면 Id를 쓴다 </summary>
        public string DisplayName { get; set; }

        /// <summary> 선택. 시스템 언어 감지·검증용 BCP-47 태그 (예: ko, en, zh-Hant). SDK 동작에 필수는 아니다 </summary>
        public string Bcp47 { get; set; }

        /// <summary> 이 언어가 비었을 때 대신 쓸 언어 Id. 비어 있으면 LanguageSet.SourceLanguageId를 쓴다 </summary>
        public string FallbackId { get; set; }

        /// <summary>
        /// 이 언어가 쓸 폰트 주소. SDK 는 문자열을 실어 나르기만 하고 뜻은 프로젝트가 정한다.
        /// 비어 있으면 FallbackId 사슬을 탄다
        /// </summary>
        public string FontAddress { get; set; }

        /// <summary> false면 동기화·익스포트 대상에서 제외한다 (미출시 언어) </summary>
        public bool Enabled { get; set; } = true;

        public LanguageDefinition()
        {
        }

        public LanguageDefinition(string id, string displayName = null, string bcp47 = null, string fallbackId = null)
        {
            Id = id;
            DisplayName = displayName;
            Bcp47 = bcp47;
            FallbackId = fallbackId;
        }

        public string ResolvedDisplayName
        {
            get { return string.IsNullOrEmpty(DisplayName) ? Id : DisplayName; }
        }

        public override string ToString()
        {
            return Id ?? "?";
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
                throw new TranslationConfigException("LanguageDefinition.Id는 비어 있을 수 없습니다.");
        }
    }
}
