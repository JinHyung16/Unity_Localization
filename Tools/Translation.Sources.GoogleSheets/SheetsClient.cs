using System;
using System.Collections.Generic;
using System.Text;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace Translation.GoogleSheets
{
    /// <summary> Sheets API 호출을 감싼다. 탭 목록을 캐싱하고 A1 범위를 조립한다 </summary>
    public sealed class SheetsClient
    {
        private readonly SheetsService _service;
        private HashSet<string> _tabs;
        private Dictionary<string, SheetProperties> _props;

        public string SpreadsheetId { get; }

        public SheetsClient(SheetsService service, string spreadsheetIdOrUrl)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            if (string.IsNullOrEmpty(spreadsheetIdOrUrl))
                throw new TranslationConfigException("spreadsheetId는 필수입니다.");

            SpreadsheetId = ExtractId(spreadsheetIdOrUrl);
        }

        public string Label
        {
            get { return "sheet:" + SpreadsheetId; }
        }

        /// <summary> 전체 URL을 넣어도 스프레드시트 ID만 뽑아낸다 </summary>
        public static string ExtractId(string idOrUrl)
        {
            const string marker = "/spreadsheets/d/";
            var index = idOrUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return idOrUrl.Trim();

            var rest = idOrUrl.Substring(index + marker.Length);
            var slash = rest.IndexOf('/');
            return (slash < 0 ? rest : rest.Substring(0, slash)).Trim();
        }

        public IReadOnlyCollection<string> Tabs
        {
            get { return EnsureTabs(); }
        }

        public bool TabExists(string tab)
        {
            return !string.IsNullOrEmpty(tab) && EnsureTabs().Contains(tab);
        }

        public void InvalidateTabCache()
        {
            _tabs = null;
            _props = null;
        }

        /// <summary> 탭을 만든다. 이미 있으면 아무것도 안 한다 </summary>
        public void AddTab(string tab)
        {
            if (TabExists(tab))
                return;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = tab } },
                    },
                },
            };

            _service.Spreadsheets.BatchUpdate(request, SpreadsheetId).Execute();
            InvalidateTabCache();
        }

        /// <summary> 탭을 비우고 격자를 통째로 쓴다. 없으면 만든다. RAW 로 넣어 수식으로 해석되지 않는다 </summary>
        public void WriteTab(string tab, IReadOnlyList<string[]> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            AddTab(tab);
            EnsureGrid(tab, rows);

            _service.Spreadsheets.Values.Clear(new ClearValuesRequest(), SpreadsheetId, QuoteTab(tab)).Execute();

            var values = new List<IList<object>>(rows.Count);
            foreach (var row in rows)
            {
                var cells = new List<object>(row?.Length ?? 0);
                if (row != null)
                {
                    foreach (var cell in row)
                        cells.Add(cell ?? string.Empty);
                }

                values.Add(cells);
            }

            if (values.Count == 0)
                return;

            var update = _service.Spreadsheets.Values.Update(
                new ValueRange { Values = values }, SpreadsheetId, QuoteTab(tab) + "!A1");
            update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            update.Execute();
        }

        /// <summary> 격자가 데이터보다 작으면 넓힌다 — Values.Update 는 격자 밖을 못 쓴다 </summary>
        private void EnsureGrid(string tab, IReadOnlyList<string[]> rows)
        {
            EnsureTabs();
            if (!_props.TryGetValue(tab, out var props) || props.SheetId == null)
                return;

            var width = 0;
            foreach (var row in rows)
                width = Math.Max(width, row?.Length ?? 0);

            var grid = props.GridProperties ?? new GridProperties();
            var rowCount = Math.Max(grid.RowCount ?? 0, rows.Count);
            var columnCount = Math.Max(grid.ColumnCount ?? 0, width);
            if (rowCount == (grid.RowCount ?? 0) && columnCount == (grid.ColumnCount ?? 0))
                return;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        UpdateSheetProperties = new UpdateSheetPropertiesRequest
                        {
                            Properties = new SheetProperties
                            {
                                SheetId = props.SheetId,
                                GridProperties = new GridProperties { RowCount = rowCount, ColumnCount = columnCount },
                            },
                            Fields = "gridProperties.rowCount,gridProperties.columnCount",
                        },
                    },
                },
            };

            _service.Spreadsheets.BatchUpdate(request, SpreadsheetId).Execute();
            InvalidateTabCache();
        }

        /// <summary> 탭 하나를 문자열 격자로 읽는다. 없으면 빈 목록 </summary>
        public List<string[]> ReadTab(string tab, int maxColumn = 200)
        {
            if (!TabExists(tab))
                return new List<string[]>();

            var request = _service.Spreadsheets.Values.Get(SpreadsheetId, Range(tab, maxColumn));
            request.ValueRenderOption =
                SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;

            return ToGrid(request.Execute()?.Values);
        }

        /// <summary> 여러 탭을 한 번의 요청으로 읽는다. API 쿼터를 아끼기 위한 경로 </summary>
        public Dictionary<string, List<string[]>> ReadTabs(IEnumerable<string> tabs, int maxColumn = 200)
        {
            var wanted = new List<string>();
            foreach (var tab in tabs)
            {
                if (TabExists(tab))
                    wanted.Add(tab);
            }

            var result = new Dictionary<string, List<string[]>>(StringComparer.Ordinal);
            if (wanted.Count == 0)
                return result;

            var request = _service.Spreadsheets.Values.BatchGet(SpreadsheetId);
            var ranges = new List<string>(wanted.Count);
            foreach (var tab in wanted)
                ranges.Add(Range(tab, maxColumn));

            request.Ranges = ranges;
            request.ValueRenderOption =
                SpreadsheetsResource.ValuesResource.BatchGetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;

            var response = request.Execute();
            if (response?.ValueRanges == null)
                return result;

            for (var i = 0; i < response.ValueRanges.Count && i < wanted.Count; i++)
                result[wanted[i]] = ToGrid(response.ValueRanges[i].Values);

            return result;
        }

        /// <summary> 1을 A로 보는 엑셀식 열 이름 </summary>
        public static string ColumnName(int oneBasedIndex)
        {
            if (oneBasedIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));

            var sb = new StringBuilder();
            var index = oneBasedIndex;
            while (index > 0)
            {
                var remainder = (index - 1) % 26;
                sb.Insert(0, (char)('A' + remainder));
                index = (index - 1) / 26;
            }

            return sb.ToString();
        }

        private HashSet<string> EnsureTabs()
        {
            if (_tabs != null)
                return _tabs;

            var request = _service.Spreadsheets.Get(SpreadsheetId);
            request.Fields = "sheets.properties(sheetId,title,gridProperties)";
            var response = request.Execute();

            _tabs = new HashSet<string>(StringComparer.Ordinal);
            _props = new Dictionary<string, SheetProperties>(StringComparer.Ordinal);
            if (response.Sheets != null)
            {
                foreach (var sheet in response.Sheets)
                {
                    if (sheet.Properties?.Title == null)
                        continue;

                    _tabs.Add(sheet.Properties.Title);
                    _props[sheet.Properties.Title] = sheet.Properties;
                }
            }

            return _tabs;
        }

        private static string QuoteTab(string tab)
        {
            return "'" + tab.Replace("'", "''") + "'";
        }

        private static string Range(string tab, int maxColumn)
        {
            return Quote(tab + "!A1:" + ColumnName(maxColumn));
        }

        private static string Quote(string range)
        {
            var bang = range.IndexOf('!');
            if (bang <= 0)
                return range;

            var tab = range.Substring(0, bang);
            if (tab.StartsWith("'", StringComparison.Ordinal))
                return range;

            return "'" + tab.Replace("'", "''") + "'" + range.Substring(bang);
        }

        private static List<string[]> ToGrid(IList<IList<object>> values)
        {
            var rows = new List<string[]>();
            if (values == null)
                return rows;

            foreach (var row in values)
            {
                if (row == null)
                {
                    rows.Add(new string[0]);
                    continue;
                }

                var cells = new string[row.Count];
                for (var i = 0; i < row.Count; i++)
                    cells[i] = row[i]?.ToString() ?? string.Empty;

                rows.Add(cells);
            }

            return rows;
        }
    }
}
