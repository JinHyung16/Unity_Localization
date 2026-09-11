using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Translation
{
    /// <summary> 설정 파일 하나를 읽은 결과. 어댑터별 섹션은 Raw에서 직접 꺼내 쓴다 </summary>
    public sealed class TranslationConfigDocument
    {
        public TranslationConfig Config { get; set; }

        /// <summary> 설정 파일 전체의 원본 맵. translations/output 같은 어댑터 섹션이 들어 있다 </summary>
        public Dictionary<string, object> Raw { get; set; }

        public string Path { get; set; }

        public Dictionary<string, object> Section(string name)
        {
            return MiniJson.GetObject(Raw, name);
        }
    }

    /// <summary> translation.config.json 을 읽는다 </summary>
    public static class TranslationConfigFile
    {
        public const string DefaultFileName = "translation.config.json";

        public static TranslationConfigDocument Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path는 필수입니다.", nameof(path));

            if (!File.Exists(path))
                throw new TranslationConfigException("설정 파일이 없습니다: " + path);

            var document = Parse(File.ReadAllText(path, Encoding.UTF8));
            document.Path = path;
            return document;
        }

        public static TranslationConfigDocument Parse(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.ParseObject(json);
            }
            catch (Exception e)
            {
                throw new TranslationConfigException("설정 파일을 JSON으로 읽을 수 없습니다: " + e.Message);
            }

            var languages = ParseLanguages(root);
            var sourceLanguage = MiniJson.GetString(root, "sourceLanguage");
            if (string.IsNullOrEmpty(sourceLanguage))
                throw new TranslationConfigException("sourceLanguage는 필수입니다.");

            var languageSet = new LanguageSet(languages, sourceLanguage);
            var config = new TranslationConfig(languageSet, ParseTargets(root), ParseKeyCodec(root));

            var naming = MiniJson.GetObject(root, "naming");
            if (naming != null)
            {
                config.TranslationSheetName =
                    MiniJson.GetString(naming, "translationSheetName", config.TranslationSheetName);
                config.TranslationSheetPrefix =
                    MiniJson.GetString(naming, "translationSheetPrefix", config.TranslationSheetPrefix);
                config.StandaloneSheetName =
                    MiniJson.GetString(naming, "standaloneSheetName", config.StandaloneSheetName);
                config.DataRowOffset = MiniJson.GetInt(naming, "dataRowOffset", config.DataRowOffset);
            }

            ApplySheetCodecs(config, root);

            return new TranslationConfigDocument { Config = config, Raw = root };
        }

        private static List<LanguageDefinition> ParseLanguages(Dictionary<string, object> root)
        {
            var array = MiniJson.GetArray(root, "languages");
            if (array == null || array.Count == 0)
                throw new TranslationConfigException("languages 배열이 필요합니다. 프로젝트가 쓰는 식별자를 그대로 선언하세요.");

            var languages = new List<LanguageDefinition>(array.Count);
            foreach (var item in array)
            {
                if (item is string plain)
                {
                    languages.Add(new LanguageDefinition(plain));
                    continue;
                }

                if (!(item is Dictionary<string, object> map))
                    throw new TranslationConfigException("languages 항목은 문자열이거나 오브젝트여야 합니다.");

                languages.Add(new LanguageDefinition
                {
                    Id = MiniJson.GetString(map, "id"),
                    DisplayName = MiniJson.GetString(map, "displayName"),
                    Bcp47 = MiniJson.GetString(map, "bcp47"),
                    FallbackId = MiniJson.GetString(map, "fallbackId"),
                    FontAddress = MiniJson.GetString(map, "font"),
                    Enabled = MiniJson.GetBool(map, "enabled", true),
                });
            }

            return languages;
        }

        private static List<TableTarget> ParseTargets(Dictionary<string, object> root)
        {
            var array = MiniJson.GetArray(root, "targets");
            if (array == null || array.Count == 0)
                throw new TranslationConfigException("targets 배열이 필요합니다.");

            var targets = new List<TableTarget>(array.Count);
            foreach (var item in array)
            {
                if (!(item is Dictionary<string, object> map))
                    throw new TranslationConfigException("targets 항목은 오브젝트여야 합니다.");

                var target = new TableTarget
                {
                    Table = MiniJson.GetString(map, "table", string.Empty),
                    IdColumn = MiniJson.GetString(map, "idColumn", "id"),
                    SubIdColumn = MiniJson.GetString(map, "subIdColumn"),
                    TranslationSheetName = MiniJson.GetString(map, "sheet"),
                    ArraySeparator = MiniJson.GetString(map, "arraySeparator"),
                    SourceTable = MiniJson.GetString(map, "sourceTable"),
                    SourceColumn = MiniJson.GetString(map, "sourceColumn"),
                };

                var columns = MiniJson.GetArray(map, "columns");
                if (columns != null)
                {
                    foreach (var column in columns)
                    {
                        if (column is string name && name.Length != 0)
                            target.Columns.Add(name);
                    }
                }

                targets.Add(target);
            }

            return targets;
        }

        /// <summary> keyCodec 이 없으면 translationKey(TranslationKey + Id). UI 시트는 CodecOf 가 LocalKey(flat) 로 돌린다 </summary>
        private static IKeyCodec ParseKeyCodec(Dictionary<string, object> root)
        {
            var map = MiniJson.GetObject(root, "keyCodec");
            return map == null ? new TranslationKeyCodec() : ParseCodecMap(map);
        }

        /// <summary> 시트별 코덱 — naming.keyCodecs 의 { 시트명: 코덱 } </summary>
        private static void ApplySheetCodecs(TranslationConfig config, Dictionary<string, object> root)
        {
            var map = MiniJson.GetObject(root, "keyCodecs");
            if (map == null)
                return;

            foreach (var pair in map)
            {
                if (pair.Value is Dictionary<string, object> codecMap)
                    config.SetSheetCodec(pair.Key, ParseCodecMap(codecMap));
            }
        }

        private static IKeyCodec ParseCodecMap(Dictionary<string, object> map)
        {
            var kind = MiniJson.GetString(map, "kind", "flat");

            switch ((kind ?? "flat").ToLowerInvariant())
            {
                case "flat":
                    return new FlatKeyCodec(
                        MiniJson.GetString(map, "keyColumn", FlatKeyCodec.DefaultKeyColumn),
                        MiniJson.GetString(map, "separator", FlatKeyCodec.DefaultSeparator));

                case "tuple":
                    return new TupleKeyCodec(
                        MiniJson.GetString(map, "tableColumn", "Table"),
                        MiniJson.GetString(map, "columnColumn", "Column"),
                        MiniJson.GetString(map, "idColumn", "FirstIndex"),
                        MiniJson.GetString(map, "subIdColumn", "SecondIndex"));

                case "translationkey":
                    return new TranslationKeyCodec(
                        MiniJson.GetString(map, "keyColumn", TranslationKeyCodec.DefaultKeyColumn),
                        MiniJson.GetString(map, "idColumn", TranslationKeyCodec.DefaultIdColumn),
                        MiniJson.GetString(map, "separator", TranslationKeyCodec.DefaultSeparator));

                default:
                    throw new TranslationConfigException(
                        "keyCodec.kind는 flat · tuple · translationKey 만 지원합니다: " + kind);
            }
        }
    }
}
