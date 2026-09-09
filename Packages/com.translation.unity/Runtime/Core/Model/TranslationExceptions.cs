using System;

namespace Translation
{
    /// <summary> 설정이 잘못됐을 때 던진다. 사용자가 고칠 수 있는 오류 </summary>
    public sealed class TranslationConfigException : Exception
    {
        public TranslationConfigException(string message) : base(message)
        {
        }
    }

    /// <summary> 번들 데이터가 손상되거나 포맷 버전이 맞지 않을 때 던진다 </summary>
    public sealed class TranslationBundleException : Exception
    {
        public TranslationBundleException(string message) : base(message)
        {
        }

        public TranslationBundleException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
