using System;

namespace Translation
{
    /// <summary> 번역 슬롯 하나의 좌표. Table/Column이 비면 독립 UI 문자열 키다 </summary>
    public struct TranslateKey : IEquatable<TranslateKey>
    {
        /// <summary> 게임 DB 테이블명. 독립 UI 문자열이면 비어 있다 </summary>
        public string Table { get; }

        /// <summary> 번역 대상 컬럼명. 독립 UI 문자열이면 비어 있다 </summary>
        public string Column { get; }

        /// <summary> 데이터 행 식별자. 독립 UI 문자열이면 그 키 이름 자체 </summary>
        public string Id { get; }

        /// <summary> 보조 식별자. 복합키 테이블에서만 쓰이고 없으면 비어 있다 </summary>
        public string SubId { get; }

        public TranslateKey(string table, string column, string id, string subId = null)
        {
            Table = Norm(table);
            Column = Norm(column);
            Id = Norm(id);
            SubId = Norm(subId);
        }

        public static TranslateKey Standalone(string id)
        {
            return new TranslateKey(null, null, id);
        }

        public bool IsStandalone
        {
            get { return Table.Length == 0 && Column.Length == 0; }
        }

        public bool HasSubId
        {
            get { return SubId.Length != 0; }
        }

        public bool IsEmpty
        {
            get { return Id.Length == 0 && IsStandalone; }
        }

        public bool Equals(TranslateKey other)
        {
            return Eq(Table, other.Table)
                && Eq(Column, other.Column)
                && Eq(Id, other.Id)
                && Eq(SubId, other.SubId);
        }

        public override bool Equals(object obj)
        {
            return obj is TranslateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var cmp = StringComparer.Ordinal;
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + cmp.GetHashCode(Table);
                hash = hash * 31 + cmp.GetHashCode(Column);
                hash = hash * 31 + cmp.GetHashCode(Id);
                hash = hash * 31 + cmp.GetHashCode(SubId);
                return hash;
            }
        }

        public override string ToString()
        {
            if (IsStandalone)
                return Id;

            return HasSubId
                ? Table + "." + Column + "[" + Id + "," + SubId + "]"
                : Table + "." + Column + "[" + Id + "]";
        }

        private static string Norm(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
