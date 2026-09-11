// .NET Standard 2.1 에는 없는 타입. C# 9 의 init 접근자와 record 가 이 이름을 찾는다.
// internal 로 두어 이 DLL 을 참조하는 쪽과 충돌하지 않게 한다.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
