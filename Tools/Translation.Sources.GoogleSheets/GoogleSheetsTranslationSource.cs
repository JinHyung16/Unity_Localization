using System;
using System.Collections.Generic;

namespace Translation.GoogleSheets
{
    /// <summary> 구글 스프레드시트를 번역 시트로 읽는다. sync 는 쓰기 스코프로 만든 서비스가 필요하다 </summary>
    public sealed class GoogleSheetsTranslationSource : ITranslationSource, ITranslationSheetStore
    {
        private readonly SheetsClient _client;

        public int MaxColumn { get; set; } = 200;

        public GoogleSheetsTranslationSource(SheetsClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string Label
        {
            get { return _client.Label; }
        }

        public bool SheetExists(string sheetName)
        {
            return _client.TabExists(sheetName);
        }

        public IReadOnlyList<TranslateEntry> Read(SheetLayout layout, IList<string> warnings)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var grid = _client.ReadTab(layout.SheetName, MaxColumn);
            if (grid.Count == 0)
                return new List<TranslateEntry>();

            var wrapped = new List<IReadOnlyList<string>>(grid.Count);
            foreach (var row in grid)
                wrapped.Add(row);

            return layout.ReadEntries(wrapped, warnings);
        }

        public List<string[]> ReadGrid(string sheetName)
        {
            return _client.ReadTab(sheetName, MaxColumn);
        }

        public void WriteGrid(string sheetName, IReadOnlyList<string[]> rows)
        {
            _client.WriteTab(sheetName, rows);
        }

        /// <summary> 여러 시트를 한 번의 BatchGet으로 읽는다. 대상이 많을 때 쿼터를 아낀다 </summary>
        public Dictionary<string, IReadOnlyList<TranslateEntry>> ReadAll(
            IEnumerable<SheetLayout> layouts,
            IList<string> warnings)
        {
            var list = new List<SheetLayout>();
            var names = new List<string>();
            foreach (var layout in layouts)
            {
                list.Add(layout);
                names.Add(layout.SheetName);
            }

            var grids = _client.ReadTabs(names, MaxColumn);
            var result = new Dictionary<string, IReadOnlyList<TranslateEntry>>(StringComparer.Ordinal);

            foreach (var layout in list)
            {
                if (!grids.TryGetValue(layout.SheetName, out var grid))
                {
                    warnings?.Add("번역 시트가 없습니다: " + layout.SheetName);
                    continue;
                }

                var wrapped = new List<IReadOnlyList<string>>(grid.Count);
                foreach (var row in grid)
                    wrapped.Add(row);

                result[layout.SheetName] = layout.ReadEntries(wrapped, warnings);
            }

            return result;
        }
    }
}
