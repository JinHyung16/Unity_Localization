using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 시트 하나의 sync 결과 </summary>
    public sealed class SheetSyncResult
    {
        public string SheetName { get; set; }

        /// <summary> 이 시트의 대상 중 원문 테이블이 하나도 없어 건너뛰었다 </summary>
        public bool Skipped { get; set; }

        public bool Existed { get; set; }

        public bool Written { get; set; }

        public int Added { get; set; }

        public int Changed { get; set; }

        /// <summary> 신규 행 중 기존 번역을 추천으로 채운 행 </summary>
        public int Suggested { get; set; }

        /// <summary> 게임 DB 에 없어 StateRemoved 로 표시한 행 </summary>
        public int Removed { get; set; }

        public int Pruned { get; set; }

        public int Unchanged { get; set; }

        public List<string> SkippedTargets { get; } = new List<string>();

        public bool HasChanges
        {
            get { return Added > 0 || Changed > 0 || Removed > 0 || Pruned > 0; }
        }

        public override string ToString()
        {
            if (Skipped)
                return SheetName + ": 건너뜀";

            return SheetName + ": 신규 " + Added + " · 원문변경 " + Changed + " · 추천 " + Suggested
                   + " · 삭제됨 " + Removed + " · 지움 " + Pruned + " · 그대로 " + Unchanged;
        }
    }

    public sealed class SyncResult
    {
        public List<SheetSyncResult> Sheets { get; } = new List<SheetSyncResult>();

        public List<string> Warnings { get; } = new List<string>();

        public int Added
        {
            get { return Sum(s => s.Added); }
        }

        public int Changed
        {
            get { return Sum(s => s.Changed); }
        }

        public int Suggested
        {
            get { return Sum(s => s.Suggested); }
        }

        public int Removed
        {
            get { return Sum(s => s.Removed); }
        }

        public int Pruned
        {
            get { return Sum(s => s.Pruned); }
        }

        public int Unchanged
        {
            get { return Sum(s => s.Unchanged); }
        }

        public bool HasChanges
        {
            get
            {
                foreach (var sheet in Sheets)
                {
                    if (sheet.HasChanges)
                        return true;
                }

                return false;
            }
        }

        private int Sum(Func<SheetSyncResult, int> pick)
        {
            var total = 0;
            foreach (var sheet in Sheets)
                total += pick(sheet);

            return total;
        }
    }
}
