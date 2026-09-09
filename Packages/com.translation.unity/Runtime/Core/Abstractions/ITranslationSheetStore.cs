using System.Collections.Generic;

namespace Translation
{
    /// <summary>
    /// 번역 시트를 셀 격자 그대로 읽고 쓴다. sync 만 쓴다.
    /// 팀이 둔 다른 컬럼을 건드리지 않으려고 격자 단위로 다룬다
    /// </summary>
    public interface ITranslationSheetStore
    {
        bool SheetExists(string sheetName);

        /// <summary> 시트 전체를 격자로. 없으면 빈 목록 </summary>
        List<string[]> ReadGrid(string sheetName);

        /// <summary> 시트를 통째로 바꿔 쓴다. 없으면 만든다 </summary>
        void WriteGrid(string sheetName, IReadOnlyList<string[]> rows);
    }
}
