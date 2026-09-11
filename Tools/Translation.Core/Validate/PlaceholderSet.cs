using System.Collections.Generic;
using System.Globalization;

namespace Translation
{
    /// <summary> 문자열에 들어 있는 string.Format 플레이스홀더 인덱스 집합 </summary>
    public static class PlaceholderSet
    {
        /// <summary> {0}, {1:N0} 같은 플레이스홀더의 인덱스를 뽑는다. 이스케이프된 {{ 는 무시한다 </summary>
        public static SortedSet<int> Extract(string text)
        {
            var result = new SortedSet<int>();
            if (string.IsNullOrEmpty(text))
                return result;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '{')
                    continue;

                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++;
                    continue;
                }

                var start = i + 1;
                var end = start;
                while (end < text.Length && char.IsDigit(text[end]))
                    end++;

                if (end == start)
                    continue;

                if (end >= text.Length)
                    continue;

                var terminator = text[end];
                if (terminator != '}' && terminator != ':' && terminator != ',')
                    continue;

                if (int.TryParse(
                        text.Substring(start, end - start),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var index))
                    result.Add(index);
            }

            return result;
        }

        public static bool SameSet(SortedSet<int> a, SortedSet<int> b)
        {
            if (a.Count != b.Count)
                return false;

            foreach (var value in a)
            {
                if (!b.Contains(value))
                    return false;
            }

            return true;
        }

        public static string Describe(SortedSet<int> set)
        {
            if (set.Count == 0)
                return "(없음)";

            var parts = new List<string>(set.Count);
            foreach (var value in set)
                parts.Add("{" + value.ToString(CultureInfo.InvariantCulture) + "}");

            return string.Join(" ", parts);
        }
    }
}
