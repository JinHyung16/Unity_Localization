using System;
using System.Collections.Generic;

namespace Translation.Excel
{
    /// <summary> 엑셀을 번역 시트로 읽는다. sync 가 쓴 시트만 저장한다 </summary>
    public sealed class ExcelTranslationSource : ITranslationSource, ITranslationSheetStore, IDisposable
    {
        private readonly ExcelWorkbookSet _workbooks;
        private readonly bool _ownsWorkbooks;

        public ExcelTranslationSource(string path)
            : this(new ExcelWorkbookSet(path), true)
        {
        }

        public ExcelTranslationSource(ExcelWorkbookSet workbooks, bool ownsWorkbooks = false)
        {
            _workbooks = workbooks ?? throw new ArgumentNullException(nameof(workbooks));
            _ownsWorkbooks = ownsWorkbooks;
        }

        public string Label
        {
            get { return _workbooks.Label; }
        }

        public bool SheetExists(string sheetName)
        {
            return _workbooks.SheetExists(sheetName);
        }

        public IReadOnlyList<TranslateEntry> Read(SheetLayout layout, IList<string> warnings)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var sheet = _workbooks.TryGetWorksheet(layout.SheetName);
            if (sheet == null)
                return new List<TranslateEntry>();

            var grid = ExcelGrid.Read(sheet);
            var wrapped = new List<IReadOnlyList<string>>(grid.Count);
            foreach (var row in grid)
                wrapped.Add(row);

            return layout.ReadEntries(wrapped, warnings);
        }

        public List<string[]> ReadGrid(string sheetName)
        {
            var sheet = _workbooks.TryGetWorksheet(sheetName);
            return sheet == null ? new List<string[]>() : ExcelGrid.Read(sheet);
        }

        public void WriteGrid(string sheetName, IReadOnlyList<string[]> rows)
        {
            ExcelGrid.Write(_workbooks.GetOrCreateWorksheet(sheetName), rows);
            _workbooks.Save();
        }

        public void Dispose()
        {
            if (_ownsWorkbooks)
                _workbooks.Dispose();
        }
    }
}
