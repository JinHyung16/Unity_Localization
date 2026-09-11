using System.Collections.Generic;

namespace Translation
{
    /// <summary> 이 시트가 어느 대상들을 담는지. 시트 하나에 표가 여럿 올 수 있어 목록이다 </summary>
    public struct KeyDecodeContext
    {
        private readonly IReadOnlyList<TableTarget> _targets;

        public IReadOnlyList<TableTarget> Targets
        {
            get { return _targets ?? Empty; }
        }

        private static readonly TableTarget[] Empty = new TableTarget[0];

        public KeyDecodeContext(IReadOnlyList<TableTarget> targets)
        {
            _targets = targets;
        }

        /// <summary> 첫 대상의 테이블명. 대상이 하나인 시트에서 쓴다 </summary>
        public string Table
        {
            get { return Targets.Count > 0 ? Targets[0].Table : string.Empty; }
        }

        /// <summary> 첫 대상의 컬럼 목록. 대상이 하나인 시트에서 쓴다 </summary>
        public IReadOnlyList<string> Columns
        {
            get { return Targets.Count > 0 ? (IReadOnlyList<string>)Targets[0].Columns : new string[0]; }
        }

        /// <summary> 이 시트가 테이블에 속하지 않는 독립 UI 문자열 세트인지 </summary>
        public bool IsStandalone
        {
            get { return Targets.Count == 1 && Targets[0].IsStandalone; }
        }

        public static KeyDecodeContext FromTarget(TableTarget target)
        {
            return new KeyDecodeContext(new[] { target });
        }

        public static KeyDecodeContext FromTargets(IReadOnlyList<TableTarget> targets)
        {
            return new KeyDecodeContext(targets);
        }

        /// <summary> 테이블명·컬럼명으로 대상을 찾는다. 못 찾으면 null </summary>
        public TableTarget Find(string table, string column)
        {
            var list = Targets;
            for (var i = 0; i < list.Count; i++)
            {
                if (!Same(list[i].Table, table))
                    continue;

                var columns = list[i].Columns;
                for (var c = 0; c < columns.Count; c++)
                {
                    if (Same(columns[c], column))
                        return list[i];
                }
            }

            return null;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty,
                System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary> TranslateKey를 번역 DB의 키 컬럼들로 인코딩하고 되돌린다 </summary>
    public interface IKeyCodec
    {
        IReadOnlyList<string> KeyColumns { get; }

        /// <summary> KeyColumns와 같은 순서, 같은 길이의 셀 값들을 만든다 </summary>
        string[] Encode(TranslateKey key);

        /// <summary> 셀 값들을 TranslateKey로 되돌린다. 해석할 수 없으면 false </summary>
        bool TryDecode(IReadOnlyList<string> cells, KeyDecodeContext context, out TranslateKey key);
    }
}
