using System;

namespace Translation
{
    /// <summary> sync 가 시트에 남기는 표식. 셀 앞에 붙는 마커와 상태 컬럼 값 </summary>
    public sealed class SyncOptions
    {
        public const string DefaultChangedMarker = "[번역수정필요]";
        public const string DefaultSuggestedMarker = "[추천데이터로 번역됨]";
        public const string DefaultStateColumn = "_State";

        /// <summary> 원문이 바뀐 행의 기존 번역 앞에 붙인다 </summary>
        public string ChangedMarker { get; set; } = DefaultChangedMarker;

        /// <summary> 같은 원문의 기존 번역을 복사해 온 셀 앞에 붙인다 </summary>
        public string SuggestedMarker { get; set; } = DefaultSuggestedMarker;

        /// <summary> 상태를 적을 컬럼. 없으면 만들고, 비우면 상태를 안 적는다 </summary>
        public string StateColumn { get; set; } = DefaultStateColumn;

        public string StateNew { get; set; } = "신규";

        public string StateChanged { get; set; } = "원문변경";

        public string StateRemoved { get; set; } = "삭제됨";

        /// <summary> 게임 DB 에서 사라진 키의 행을 지운다. 기본은 StateRemoved 로 표시만 한다 </summary>
        public bool Prune { get; set; }

        /// <summary> 계산만 하고 시트에 쓰지 않는다 </summary>
        public bool DryRun { get; set; }

        public bool IsMarked(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return StartsWith(value, ChangedMarker) || StartsWith(value, SuggestedMarker);
        }

        private static bool StartsWith(string value, string marker)
        {
            return !string.IsNullOrEmpty(marker) && value.StartsWith(marker, StringComparison.Ordinal);
        }
    }
}
