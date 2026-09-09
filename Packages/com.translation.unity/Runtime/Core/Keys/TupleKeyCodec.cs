using System.Collections.Generic;

namespace Translation
{
    /// <summary> 키를 Table, Column, Id, SubId 네 컬럼으로 나눠 담는다. dfw-client의 TranslateKey 규약 </summary>
    public sealed class TupleKeyCodec : IKeyCodec
    {
        private readonly string[] _keyColumns;

        public TupleKeyCodec(
            string tableColumn = "Table",
            string columnColumn = "Column",
            string idColumn = "FirstIndex",
            string subIdColumn = "SecondIndex")
        {
            _keyColumns = new[] { tableColumn, columnColumn, idColumn, subIdColumn };
            foreach (var name in _keyColumns)
            {
                if (string.IsNullOrEmpty(name))
                    throw new TranslationConfigException("TupleKeyCodec의 키 컬럼 이름은 모두 필수입니다.");
            }
        }

        public IReadOnlyList<string> KeyColumns
        {
            get { return _keyColumns; }
        }

        public string[] Encode(TranslateKey key)
        {
            return new[] { key.Table, key.Column, key.Id, key.SubId };
        }

        public bool TryDecode(IReadOnlyList<string> cells, KeyDecodeContext context, out TranslateKey key)
        {
            key = default;
            if (cells == null || cells.Count < 3)
                return false;

            var id = (cells[2] ?? string.Empty).Trim();
            if (id.Length == 0)
                return false;

            var subId = cells.Count > 3 ? cells[3] : null;
            key = new TranslateKey(cells[0], cells[1], id, subId);
            return true;
        }
    }
}
