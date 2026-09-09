using System;
using System.Collections.Generic;
using System.Text;

namespace Translation
{
    /// <summary> 키를 단일 문자열 컬럼에 담는다. project_mining의 Table_Column_Id 규약 </summary>
    public sealed class FlatKeyCodec : IKeyCodec
    {
        public const string DefaultSeparator = "_";
        public const string DefaultKeyColumn = "id";

        private readonly string[] _keyColumns;

        public string Separator { get; }

        public FlatKeyCodec(string keyColumn = DefaultKeyColumn, string separator = DefaultSeparator)
        {
            if (string.IsNullOrEmpty(keyColumn))
                throw new TranslationConfigException("FlatKeyCodec의 keyColumn은 필수입니다.");

            if (string.IsNullOrEmpty(separator))
                throw new TranslationConfigException("FlatKeyCodec의 separator는 필수입니다.");

            _keyColumns = new[] { keyColumn };
            Separator = separator;
        }

        public IReadOnlyList<string> KeyColumns
        {
            get { return _keyColumns; }
        }

        public string[] Encode(TranslateKey key)
        {
            return new[] { Flatten(key) };
        }

        /// <summary> Table, Column, Id를 구분자로 이어 붙인다. 독립 키는 Id만 쓴다 </summary>
        public string Flatten(TranslateKey key)
        {
            if (key.IsStandalone)
                return key.HasSubId ? key.Id + Separator + key.SubId : key.Id;

            var sb = new StringBuilder();
            sb.Append(key.Table).Append(Separator);
            sb.Append(key.Column).Append(Separator);
            sb.Append(key.Id);
            if (key.HasSubId)
                sb.Append(Separator).Append(key.SubId);

            return sb.ToString();
        }

        public bool TryDecode(IReadOnlyList<string> cells, KeyDecodeContext context, out TranslateKey key)
        {
            key = default;
            if (cells == null || cells.Count == 0)
                return false;

            var raw = (cells[0] ?? string.Empty).Trim();
            if (raw.Length == 0)
                return false;

            // 시트가 담는 대상 전부와 대조한다 — 한 시트에 표가 여럿 올 수 있다
            var targets = context.Targets;
            var hasStandalone = targets.Count == 0;
            for (var t = 0; t < targets.Count; t++)
            {
                var target = targets[t];
                if (target.IsStandalone)
                {
                    hasStandalone = true;
                    continue;
                }

                var columns = target.Columns;
                for (var i = 0; i < columns.Count; i++)
                {
                    var prefix = target.Table + Separator + columns[i] + Separator;
                    if (!raw.StartsWith(prefix, StringComparison.Ordinal))
                        continue;

                    var id = raw.Substring(prefix.Length);
                    if (id.Length == 0)
                        return false;

                    key = new TranslateKey(target.Table, columns[i], id);
                    return true;
                }
            }

            if (hasStandalone)
            {
                key = TranslateKey.Standalone(raw);
                return true;
            }

            return false;
        }
    }
}
