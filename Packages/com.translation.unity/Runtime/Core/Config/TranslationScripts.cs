namespace Translation
{
    /// <summary> 더블클릭으로 명령을 돌리는 bat. CLI 의 init 과 Unity 의 설정 에셋이 같은 내용을 쓴다 </summary>
    public static class TranslationScripts
    {
        public const string SyncFileName = "sync.bat";
        public const string ValidateFileName = "validate.bat";
        public const string ExportFileName = "export.bat";

        public static string SyncBat
        {
            get { return Bat("sync"); }
        }

        public static string ValidateBat
        {
            get { return Bat("validate"); }
        }

        public static string ExportBat
        {
            get { return Bat("export"); }
        }

        /// <summary> 같은 폴더의 translation.exe 를 먼저 찾고, 없으면 PATH 의 translation 을 쓴다 </summary>
        public static string Bat(string command)
        {
            return "@echo off\r\n"
                   + "cd /d \"%~dp0\"\r\n"
                   + "set CLI=%~dp0translation.exe\r\n"
                   + "if not exist \"%CLI%\" set CLI=translation\r\n"
                   + "\"%CLI%\" " + command + " --verbose %*\r\n"
                   + "pause\r\n";
        }
    }
}
