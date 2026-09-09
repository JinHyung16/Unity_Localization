using System;
using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace Translation.GoogleSheets
{
    /// <summary> SheetsService 를 만든다. writable 이면 쓰기 스코프를, 아니면 읽기 전용을 요청한다 </summary>
    public static class GoogleSheetsAuth
    {
        public const string DefaultApplicationName = "Translation";

        /// <summary> 자격증명 JSON 의 종류를 보고 서비스 계정과 OAuth 중 맞는 쪽으로 만든다 </summary>
        public static SheetsService Create(
            string credentialsJsonPath,
            bool writable = false,
            string applicationName = DefaultApplicationName,
            string tokenStoreDir = null)
        {
            if (string.IsNullOrEmpty(credentialsJsonPath))
                throw new TranslationConfigException("credentials 경로가 필요합니다.");

            if (!File.Exists(credentialsJsonPath))
                throw new TranslationConfigException("자격증명 JSON이 없습니다: " + credentialsJsonPath);

            return IsServiceAccount(credentialsJsonPath)
                ? CreateWithServiceAccount(credentialsJsonPath, writable, applicationName)
                : CreateWithOAuth(credentialsJsonPath, writable, applicationName, tokenStoreDir);
        }

        public static bool IsServiceAccount(string credentialsJsonPath)
        {
            var map = MiniJson.ParseObject(File.ReadAllText(credentialsJsonPath));
            return string.Equals(MiniJson.GetString(map, "type"), "service_account", StringComparison.Ordinal);
        }

        public static SheetsService CreateWithServiceAccount(
            string credentialsJsonPath,
            bool writable = false,
            string applicationName = DefaultApplicationName)
        {
            GoogleCredential credential;
            using (var stream = new FileStream(credentialsJsonPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scope(writable));
            }

            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
        }

        /// <summary> 설치형 앱 OAuth. 첫 실행 때 브라우저 동의창이 뜨고 토큰이 캐시된다 </summary>
        public static SheetsService CreateWithOAuth(
            string credentialsJsonPath,
            bool writable = false,
            string applicationName = DefaultApplicationName,
            string tokenStoreDir = null)
        {
            var tokenDir = string.IsNullOrEmpty(tokenStoreDir) ? DefaultTokenStoreDir() : tokenStoreDir;
            Directory.CreateDirectory(tokenDir);

            UserCredential credential;
            using (var stream = new FileStream(credentialsJsonPath, FileMode.Open, FileAccess.Read))
            {
                var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    new[] { Scope(writable) },
                    writable ? "translation-write" : "translation-read",
                    CancellationToken.None,
                    new FileDataStore(tokenDir, true)).GetAwaiter().GetResult();
            }

            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });
        }

        private static string Scope(bool writable)
        {
            return writable ? SheetsService.Scope.Spreadsheets : SheetsService.Scope.SpreadsheetsReadonly;
        }

        private static string DefaultTokenStoreDir()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root))
                root = Path.GetTempPath();

            return Path.Combine(root, "Translation", "Tokens");
        }
    }
}
