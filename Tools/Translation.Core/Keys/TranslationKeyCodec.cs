using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary>
    /// 키를 TranslationKey(시트명_컬럼명) 와 Id 두 컬럼에 담는다.
    /// 시트 하나가 여러 표를 담을 수 있다
    /// </summary>
    public sealed class TranslationKeyCodec : IKeyCodec
    {
        public const string DefaultKeyColumn = "TranslationKey";
        public const string DefaultIdColumn = "Id";
        public const string DefaultSeparator = "_";

        private readonly string[] _keyColumns;

        public string Separator { get; }

        public TranslationKeyCodec(
            string keyColumn = DefaultKeyColumn,
            string idColumn = DefaultIdColumn,
            string separator = DefaultSeparator)
        {
            if (string.IsNullOrEmpty(keyColumn))
                throw new TranslationConfigException("TranslationKeyCodec의 keyColumn은 필수입니다.");

            if (string.IsNullOrEmpty(idColumn))
                throw new TranslationConfigException("TranslationKeyCodec의 idColumn은 필수입니다.");

            if (string.IsNullOrEmpty(separator))
                throw new TranslationConfigException("TranslationKeyCodec의 separator는 필수입니다.");

            _keyColumns = new[] { keyColumn, idColumn };
            Separator = separator;
        }

        public IReadOnlyList<string> KeyColumns
        {
            get { return _keyColumns; }
        }

        /// <summary> 시트명_컬럼명. 행 식별자는 별개 컬럼이라 여기 안 들어간다 </summary>
        public string Flatten(TranslateKey key)
        {
            return key.Table + Separator + key.Column;
        }

        public string[] Encode(TranslateKey key)
        {
            var id = key.HasSubId ? key.Id + Separator + key.SubId : key.Id;
            return new[] { Flatten(key), id };
        }

        public bool TryDecode(IReadOnlyList<string> cells, KeyDecodeContext context, out TranslateKey key)
        {
            key = default;
            if (cells == null || cells.Count < 2)
                return false;

            var raw = (cells[0] ?? string.Empty).Trim();
            var id = (cells[1] ?? string.Empty).Trim();
            if (raw.Length == 0 || id.Length == 0)
                return false;

            // 선언된 대상과 대조해 표·컬럼을 가른다. 이름에 구분자가 들어 있어도 안 깨진다
            var targets = context.Targets;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.IsStandalone)
                    continue;

                var columns = target.Columns;
                for (var c = 0; c < columns.Count; c++)
                {
                    var expected = target.Table + Separator + columns[c];
                    if (!string.Equals(raw, expected, StringComparison.Ordinal))
                        continue;

                    key = new TranslateKey(target.Table, columns[c], id);
                    return true;
                }
            }

            return false;
        }
    }
}
