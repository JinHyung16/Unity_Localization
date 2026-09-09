using System;
using System.Text;

namespace Translation
{
    /// <summary>
    /// 번들의 키 문자열 규약. 키 코덱과 무관하게 늘 같은 문자열이 나온다.
    /// UI 문자열 키는 Id 그대로라 코드에서 바로 조회할 수 있다
    /// </summary>
    public static class BundleKey
    {
        /// <summary> Table/Column/Id/SubId를 구분하는 문자. 데이터에 등장할 수 없는 유닛 구분자를 쓴다 </summary>
        public const char Separator = '\u001f';

        public static string Encode(TranslateKey key)
        {
            if (key.IsStandalone && !key.HasSubId)
                return key.Id;

            var sb = new StringBuilder();
            sb.Append(key.Table).Append(Separator);
            sb.Append(key.Column).Append(Separator);
            sb.Append(key.Id);
            if (key.HasSubId)
                sb.Append(Separator).Append(key.SubId);

            return sb.ToString();
        }

        public static string Encode(string table, string column, string id, string subId = null)
        {
            return Encode(new TranslateKey(table, column, id, subId));
        }

        public static TranslateKey Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return default;

            var parts = encoded.Split(Separator);
            switch (parts.Length)
            {
                case 1:
                    return TranslateKey.Standalone(parts[0]);
                case 3:
                    return new TranslateKey(parts[0], parts[1], parts[2]);
                case 4:
                    return new TranslateKey(parts[0], parts[1], parts[2], parts[3]);
                default:
                    throw new TranslationBundleException("번들 키 형식이 잘못됐습니다: " + encoded.Replace(Separator, '|'));
            }
        }

        /// <summary> 로그와 리포트에 보여줄 사람이 읽는 형태 </summary>
        public static string ToDisplay(string encoded)
        {
            return encoded == null ? string.Empty : encoded.Replace(Separator, '/');
        }
    }
}
