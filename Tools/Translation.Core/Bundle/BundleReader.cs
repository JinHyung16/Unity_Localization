using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Translation
{
    /// <summary> 번들 바이트를 해석한다. gzip 여부는 자동으로 판별한다 </summary>
    public static class BundleReader
    {
        public static BundleManifest ReadManifest(byte[] bytes)
        {
            return BundleManifest.FromJson(ReadText(bytes));
        }

        public static LanguageBundle ReadLanguage(byte[] bytes)
        {
            return LanguageBundle.FromJson(ReadText(bytes));
        }

        /// <summary> 받은 바이트가 매니페스트에 적힌 해시와 일치하는지 </summary>
        public static bool VerifyHash(byte[] bytes, string expectedHash)
        {
            if (bytes == null || string.IsNullOrEmpty(expectedHash))
                return false;

            return string.Equals(
                BundleBuilder.Sha256Hex(bytes),
                expectedHash,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> gzip이면 풀고 UTF-8로 읽는다 </summary>
        public static string ReadText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new TranslationBundleException("번들 바이트가 비어 있습니다.");

            if (!IsGzip(bytes))
                return DecodeUtf8(bytes);

            try
            {
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return DecodeUtf8(output.ToArray());
                }
            }
            catch (Exception e)
            {
                throw new TranslationBundleException("gzip 번들을 풀 수 없습니다: " + e.Message, e);
            }
        }

        public static bool IsGzip(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b;
        }

        private static string DecodeUtf8(byte[] bytes)
        {
            var offset = HasUtf8Bom(bytes) ? 3 : 0;
            return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }
    }
}
