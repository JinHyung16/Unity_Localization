using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Translation
{
    public sealed class InjectReport
    {
        public int Injected { get; set; }

        /// <summary> 번들에 키가 없어 건너뛴 필드 수 </summary>
        public int MissingKeys { get; set; }

        public int RowCount { get; set; }

        public List<string> Warnings { get; } = new List<string>();

        public void Merge(InjectReport other)
        {
            if (other == null)
                return;

            Injected += other.Injected;
            MissingKeys += other.MissingKeys;
            RowCount += other.RowCount;
            Warnings.AddRange(other.Warnings);
        }

        public override string ToString()
        {
            return "injected=" + Injected + ", missing=" + MissingKeys + ", rows=" + RowCount
                   + ", warnings=" + Warnings.Count;
        }
    }

    /// <summary> 게임 데이터 객체의 번역 대상 필드를 지금 언어 값으로 덮어쓴다 </summary>
    public sealed class FieldInjector
    {
        private readonly TranslationCatalog _catalog;

        public FieldInjector(TranslationCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// 한 테이블의 행들에 번역을 넣는다. idSelector 가 없으면
        /// target.IdColumn 이름의 필드나 프로퍼티를 리플렉션으로 읽는다
        /// </summary>
        public InjectReport Inject(
            TableTarget target,
            IEnumerable rows,
            Func<object, string> idSelector = null,
            Func<object, string> subIdSelector = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var report = new InjectReport();
            if (rows == null)
                return report;

            if (target.IsStandalone)
            {
                report.Warnings.Add("독립 UI 문자열 대상은 필드 주입 대상이 아닙니다. 카탈로그로 직접 조회하세요.");
                return report;
            }

            var members = new Dictionary<string, MemberAccessor>(StringComparer.Ordinal);
            MemberAccessor idMember = null;
            MemberAccessor subIdMember = null;
            Type rowType = null;

            foreach (var row in rows)
            {
                if (row == null)
                    continue;

                report.RowCount++;

                if (rowType != row.GetType())
                {
                    rowType = row.GetType();
                    members.Clear();
                    idMember = idSelector != null ? null : Require(rowType, target.IdColumn, report);
                    subIdMember = subIdSelector != null || !target.HasSubId
                        ? null
                        : Require(rowType, target.SubIdColumn, report);

                    foreach (var column in target.Columns)
                    {
                        var accessor = Require(rowType, column, report);
                        if (accessor == null)
                            continue;

                        if (!accessor.CanWrite)
                        {
                            report.Warnings.Add(rowType.Name + "." + column + ": 쓸 수 없는 멤버입니다.");
                            continue;
                        }

                        members[column] = accessor;
                    }
                }

                var id = idSelector != null ? idSelector(row) : ToId(idMember?.GetValue(row));
                if (string.IsNullOrEmpty(id))
                    continue;

                var subId = subIdSelector != null
                    ? subIdSelector(row)
                    : ToId(subIdMember?.GetValue(row));

                foreach (var pair in members)
                {
                    var key = target.MakeKey(pair.Key, id, subId);
                    if (!_catalog.TryGet(key, out var value))
                    {
                        report.MissingKeys++;
                        continue;
                    }

                    if (Assign(pair.Value, row, value, target, report))
                        report.Injected++;
                }
            }

            return report;
        }

        private static bool Assign(
            MemberAccessor accessor,
            object row,
            string value,
            TableTarget target,
            InjectReport report)
        {
            var type = accessor.ValueType;

            if (type == typeof(string))
            {
                accessor.SetValue(row, value);
                return true;
            }

            if (type == typeof(string[]))
            {
                if (!target.IsArray)
                {
                    report.Warnings.Add(
                        accessor.Name + ": string[] 멤버인데 arraySeparator가 선언되지 않았습니다.");
                    return false;
                }

                accessor.SetValue(row, value.Split(new[] { target.ArraySeparator }, StringSplitOptions.None));
                return true;
            }

            if (type == typeof(List<string>))
            {
                if (!target.IsArray)
                {
                    report.Warnings.Add(
                        accessor.Name + ": List<string> 멤버인데 arraySeparator가 선언되지 않았습니다.");
                    return false;
                }

                accessor.SetValue(
                    row,
                    new List<string>(value.Split(new[] { target.ArraySeparator }, StringSplitOptions.None)));
                return true;
            }

            report.Warnings.Add(accessor.Name + ": 주입할 수 없는 타입입니다: " + type.Name);
            return false;
        }

        private static MemberAccessor Require(Type type, string name, InjectReport report)
        {
            var accessor = MemberAccessor.Find(type, name);
            if (accessor == null)
                report.Warnings.Add(type.Name + ": 멤버 " + name + "을 찾을 수 없습니다.");

            return accessor;
        }

        private static string ToId(object value)
        {
            if (value == null)
                return null;

            if (value is string s)
                return s;

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }
    }
}
