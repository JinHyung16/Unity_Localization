using System.Collections.Generic;

namespace Translation
{
    /// <summary> 번역 시트를 읽는다. 쓰는 것은 <see cref="ITranslationSheetStore"/> 가 한다 </summary>
    public interface ITranslationSource
    {
        bool SheetExists(string sheetName);

        IReadOnlyList<TranslateEntry> Read(SheetLayout layout, IList<string> warnings);
    }
}
