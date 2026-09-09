using UnityEngine;

namespace Translation.Unity
{
    /// <summary>
    /// enum 필드에 붙이면 인스펙터에서 검색창이 있는 드롭다운으로 고른다.
    /// 항목이 수천 개인 LocalKey 같은 enum 용이다
    /// </summary>
    public sealed class SearchableEnumAttribute : PropertyAttribute
    {
    }
}
