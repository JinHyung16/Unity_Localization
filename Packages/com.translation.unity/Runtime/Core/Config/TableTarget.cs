using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 번역 대상 선언 하나. 게임 DB의 어느 테이블 어느 컬럼을 번역할지 </summary>
    public sealed class TableTarget
    {
        /// <summary> 게임 DB 테이블명. 비어 있으면 테이블에 속하지 않는 독립 UI 문자열 세트다 </summary>
        public string Table { get; set; }

        public string IdColumn { get; set; } = "id";

        /// <summary> 보조 식별자 컬럼명. 복합키 테이블에서만 쓴다 </summary>
        public string SubIdColumn { get; set; }

        public List<string> Columns { get; set; } = new List<string>();

        /// <summary> 번역 DB 쪽 시트 이름. 비어 있으면 TranslationConfig의 규약으로 만든다 </summary>
        public string TranslationSheetName { get; set; }

        /// <summary> 배열 컬럼을 한 셀에 합칠 때 쓰는 구분자. 비어 있으면 배열이 아니다 </summary>
        public string ArraySeparator { get; set; }

        /// <summary> sync 가 원문을 읽을 게임 DB 테이블. 비우면 Table. 독립 UI 세트는 이걸 줘야 sync 가 돈다 </summary>
        public string SourceTable { get; set; }

        /// <summary> 독립 UI 세트에서 원문 텍스트가 든 컬럼. 비우면 원문 언어 Id 와 같은 이름 </summary>
        public string SourceColumn { get; set; }

        /// <summary> sync 가 실제로 읽을 테이블. 없으면 빈 문자열 </summary>
        public string ResolvedSourceTable
        {
            get { return !string.IsNullOrEmpty(SourceTable) ? SourceTable : (Table ?? string.Empty); }
        }

        public bool IsStandalone
        {
            get { return string.IsNullOrEmpty(Table); }
        }

        public bool HasSubId
        {
            get { return !string.IsNullOrEmpty(SubIdColumn); }
        }

        public bool IsArray
        {
            get { return !string.IsNullOrEmpty(ArraySeparator); }
        }

        public TableTarget()
        {
        }

        public TableTarget(string table, params string[] columns)
        {
            Table = table;
            if (columns != null)
                Columns = new List<string>(columns);
        }

        public TranslateKey MakeKey(string column, string id, string subId = null)
        {
            return IsStandalone ? TranslateKey.Standalone(id) : new TranslateKey(Table, column, id, subId);
        }

        internal void Validate()
        {
            if (Columns == null)
                Columns = new List<string>();

            if (!IsStandalone && Columns.Count == 0)
                throw new TranslationConfigException("대상 " + Table + "에 번역 컬럼이 선언되지 않았습니다.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in Columns)
            {
                if (string.IsNullOrWhiteSpace(column))
                    throw new TranslationConfigException("대상 " + Table + "에 빈 컬럼명이 있습니다.");

                if (!seen.Add(column))
                    throw new TranslationConfigException("대상 " + Table + "에 컬럼명이 중복입니다: " + column);
            }

            if (!IsStandalone && string.IsNullOrWhiteSpace(IdColumn))
                throw new TranslationConfigException("대상 " + Table + "의 idColumn은 필수입니다.");
        }

        public override string ToString()
        {
            return IsStandalone
                ? "(standalone)"
                : Table + " [" + string.Join(", ", Columns) + "]";
        }
    }
}
