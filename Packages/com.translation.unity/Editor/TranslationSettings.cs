using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Translation.Unity.Editor
{
    /// <summary>
    /// 번역 설정을 Unity 안에서 적는 자리. 어디에 만들어도 된다.
    /// 인스펙터 버튼으로 sync · validate · export 를 돌리고,
    /// 같은 내용을 translation.config.json 으로 뽑아 exe 로 돌릴 수도 있다
    /// </summary>
    [CreateAssetMenu(menuName = "Translation/Translation Settings", fileName = "TranslationSettings")]
    public sealed class TranslationSettings : ScriptableObject
    {
        public enum GameDataType
        {
            Json,
            Csv,
        }

        [Serializable]
        public sealed class Language
        {
            [Tooltip("시트의 언어 열 이름 = 결과 파일 이름")]
            public string Id = "Korean";

            [Tooltip("언어 선택 UI 에 띄울 이름. 비우면 Id")]
            public string DisplayName;

            [Tooltip("기기 언어 자동 감지용 (ko, en, zh-Hant). 없어도 된다")]
            public string Bcp47;

            [Tooltip("번역이 없을 때 대신 쓸 언어 Id. 비우면 원문 언어")]
            public string FallbackId;

            [Tooltip("이 언어가 쓸 폰트 주소. 없으면 대체 언어 것을 쓴다")]
            public string Font;

            public bool Enabled = true;
        }

        [Serializable]
        public sealed class Target
        {
            [Tooltip("게임 DB 테이블 이름. 비우면 UI 문자열 세트")]
            public string Table;

            [Tooltip("행 식별자 컬럼. UI 세트에서는 키 이름이 든 컬럼")]
            public string IdColumn = "Id";

            [Tooltip("보조 식별자 컬럼. 두 컬럼이 합쳐져야 한 행이 정해지는 표에서만")]
            public string SubIdColumn;

            [Tooltip("번역할 컬럼들")]
            public string[] Columns = new string[0];

            [Tooltip("문자열 배열 컬럼을 한 셀에 이을 구분자")]
            public string ArraySeparator;

            [Tooltip("sync 가 원문을 읽을 테이블. 비우면 Table. UI 세트는 이걸 줘야 sync 가 돈다")]
            public string SourceTable;

            [Tooltip("UI 세트에서 원문이 든 컬럼. 비우면 원문 언어와 같은 이름")]
            public string SourceColumn;

            [Tooltip("이 대상이 든 시트 이름. 비우면 기본 시트")]
            public string Sheet;
        }

        [Header("언어")]
        public string SourceLanguage = "Korean";

        public List<Language> Languages = new List<Language>
        {
            new Language { Id = "Korean", DisplayName = "한국어", Bcp47 = "ko" },
            new Language { Id = "English", DisplayName = "English", Bcp47 = "en", FallbackId = "Korean" },
        };

        [Header("폴더")]
        [Tooltip("원문을 읽을 게임 DB 폴더. 테이블 하나 = 파일 하나")]
        public DefaultAsset GameDataFolder;

        public GameDataType GameDataFormat = GameDataType.Json;

        [Tooltip("번역 CSV 가 놓일 폴더. 비우면 이 에셋이 있는 폴더")]
        public DefaultAsset SheetFolder;

        [Tooltip("번역 JSON 을 넣을 폴더. 비우면 이 에셋 옆의 Bundles/")]
        public DefaultAsset OutputFolder;

        [Header("번역할 것")]
        public List<Target> Targets = new List<Target>
        {
            new Target { Table = string.Empty, SourceTable = "LocalizationData", IdColumn = "Key" },
        };

        [Header("코드 생성")]
        [Tooltip("Export 때 LocalKey.cs(enum) 와 LocalizeText.cs(TMP 컴포넌트) 를 넣을 폴더. 비우면 만들지 않는다")]
        public DefaultAsset ScriptFolder;

        [Tooltip("생성 코드의 네임스페이스")]
        public string ScriptNamespace = LocalKeyScript.DefaultNamespace;

        [Header("시트")]
        [Tooltip("헤더 다음에 건너뛸 행 수. 2행이 타입 행이면 1")]
        public int DataRowOffset;

        [Tooltip("export 에 적을 버전. 비우면 UTC 시각")]
        public string BundleVersion;

        /// <summary> 이 에셋이 있는 폴더 (절대 경로). translation.config.json 과 bat 이 여기 생긴다 </summary>
        public string ConfigDirectory
        {
            get
            {
                var assetPath = AssetDatabase.GetAssetPath(this);
                var directory = string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath);
                return ToAbsolute(directory);
            }
        }

        public string ConfigFilePath
        {
            get { return Path.Combine(ConfigDirectory, TranslationConfigFile.DefaultFileName); }
        }

        public string GameDataDirectory
        {
            get { return FolderPath(GameDataFolder, null); }
        }

        public string SheetDirectory
        {
            get { return FolderPath(SheetFolder, ConfigDirectory); }
        }

        public string OutputDirectory
        {
            get { return FolderPath(OutputFolder, Path.Combine(ConfigDirectory, "Bundles")); }
        }

        /// <summary> 생성 코드를 넣을 폴더. 안 정했으면 null </summary>
        public string ScriptDirectory
        {
            get { return FolderPath(ScriptFolder, null); }
        }

        /// <summary> 이 설정을 translation.config.json 내용으로. 경로는 설정 파일 기준 상대 경로 </summary>
        public string ToConfigJson()
        {
            var configDirectory = ConfigDirectory;
            var languages = new List<object>();
            foreach (var language in Languages)
            {
                var map = new Dictionary<string, object> { { "id", language.Id } };
                Put(map, "displayName", language.DisplayName);
                Put(map, "bcp47", language.Bcp47);
                Put(map, "fallbackId", language.FallbackId);
                Put(map, "font", language.Font);
                if (!language.Enabled)
                    map["enabled"] = false;

                languages.Add(map);
            }

            var targets = new List<object>();
            foreach (var target in Targets)
            {
                var map = new Dictionary<string, object> { { "table", target.Table ?? string.Empty } };
                Put(map, "idColumn", target.IdColumn);
                Put(map, "subIdColumn", target.SubIdColumn);
                if (target.Columns != null && target.Columns.Length > 0)
                    map["columns"] = new List<object>(target.Columns);

                Put(map, "arraySeparator", target.ArraySeparator);
                Put(map, "sourceTable", target.SourceTable);
                Put(map, "sourceColumn", target.SourceColumn);
                Put(map, "sheet", target.Sheet);
                targets.Add(map);
            }

            var root = new Dictionary<string, object>
            {
                { "sourceLanguage", SourceLanguage },
                { "languages", languages },
                { "naming", new Dictionary<string, object> { { "dataRowOffset", DataRowOffset } } },
                { "translations", new Dictionary<string, object>
                    {
                        { "type", "csv" },
                        { "path", Relative(configDirectory, SheetDirectory) },
                    } },
                { "output", new Dictionary<string, object>
                    {
                        { "directory", Relative(configDirectory, OutputDirectory) },
                    } },
                { "targets", targets },
            };

            var gameData = GameDataDirectory;
            if (!string.IsNullOrEmpty(gameData))
            {
                root["gameData"] = new Dictionary<string, object>
                {
                    { "type", GameDataFormat == GameDataType.Json ? "json" : "csv" },
                    { "path", Relative(configDirectory, gameData) },
                };
            }

            var script = ScriptDirectory;
            if (!string.IsNullOrEmpty(script))
            {
                root["script"] = new Dictionary<string, object>
                {
                    { "directory", Relative(configDirectory, script) },
                    { "namespace", string.IsNullOrWhiteSpace(ScriptNamespace) ? LocalKeyScript.DefaultNamespace : ScriptNamespace.Trim() },
                };
            }

            return MiniJson.Write(root, true);
        }

        private static void Put(Dictionary<string, object> map, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                map[key] = value;
        }

        private static string FolderPath(DefaultAsset folder, string fallback)
        {
            if (folder == null)
                return fallback;

            var assetPath = AssetDatabase.GetAssetPath(folder);
            return string.IsNullOrEmpty(assetPath) ? fallback : ToAbsolute(assetPath);
        }

        /// <summary> "Assets/…" 를 절대 경로로 </summary>
        public static string ToAbsolute(string projectRelative)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelative));
        }

        private static string Relative(string from, string to)
        {
            var relative = Path.GetRelativePath(from, to).Replace('\\', '/');
            return relative == "." ? "." : "./" + relative;
        }
    }
}
