using System;
using System.Collections.Generic;

namespace Translation
{
    /// <summary> 번역 시트를 어떻게 읽을지. 언어 목록, 읽을 시트와 컬럼, 키 규약 </summary>
    public sealed class TranslationConfig
    {
        public const string DefaultSheetPrefix = "Localization_";
        public const string DefaultStandaloneSheetName = "LocalizationData";
        public const string DefaultTranslationSheetName = "TranslationData";
        public const string DefaultStandaloneKeyColumn = "LocalKey";

        public LanguageSet Languages { get; }

        public IReadOnlyList<TableTarget> Targets { get; }

        /// <summary> 시트별 코덱이 없을 때 쓰는 기본 코덱 </summary>
        public IKeyCodec KeyCodec { get; }

        private readonly Dictionary<string, IKeyCodec> _codecBySheet =
            new Dictionary<string, IKeyCodec>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _sheetOrder = new List<string>();

        private readonly Dictionary<string, List<TableTarget>> _targetsBySheet =
            new Dictionary<string, List<TableTarget>>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 표 번역이 전부 들어가는 시트. 비우면 표마다 TranslationSheetPrefix + 테이블명 </summary>
        public string TranslationSheetName { get; set; } = DefaultTranslationSheetName;

        /// <summary> TranslationSheetName 이 비어 있을 때, sheet 를 안 적은 대상의 시트 이름에 붙이는 접두사 </summary>
        public string TranslationSheetPrefix { get; set; } = DefaultSheetPrefix;

        /// <summary> UI 문자열 세트의 시트 이름. 기본 LocalizationData </summary>
        public string StandaloneSheetName { get; set; } = DefaultStandaloneSheetName;

        /// <summary> 헤더 다음에 건너뛸 행 수. 2행이 타입 행이면 1 </summary>
        public int DataRowOffset { get; set; }

        public TranslationConfig(LanguageSet languages, IEnumerable<TableTarget> targets, IKeyCodec keyCodec = null)
        {
            Languages = languages ?? throw new ArgumentNullException(nameof(languages));
            KeyCodec = keyCodec ?? new FlatKeyCodec();

            var list = new List<TableTarget>();
            var seenSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var standaloneCount = 0;

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    target.Validate();
                    if (target.IsStandalone)
                        standaloneCount++;

                    list.Add(target);
                }
            }

            if (standaloneCount > 1)
                throw new TranslationConfigException("독립 UI 문자열 대상은 하나만 선언할 수 있습니다.");

            Targets = list;
            seenSheets.Clear();
        }

        /// <summary> 시트 이름을 선언 순서로. 대상이 같은 시트를 공유하면 한 번만 나온다 </summary>
        public IReadOnlyList<string> SheetNames
        {
            get
            {
                BuildGroups();
                return _sheetOrder;
            }
        }

        public IReadOnlyList<TableTarget> TargetsOf(string sheetName)
        {
            BuildGroups();
            return _targetsBySheet.TryGetValue(sheetName ?? string.Empty, out var list)
                ? list
                : new List<TableTarget>();
        }

        public void SetSheetCodec(string sheetName, IKeyCodec codec)
        {
            if (string.IsNullOrEmpty(sheetName) || codec == null)
                return;

            _codecBySheet[sheetName] = codec;
        }

        /// <summary> 시트의 코덱. UI 문자열 시트에 표용 코덱이 걸리면 LocalKey 한 컬럼으로 읽는다 </summary>
        public IKeyCodec CodecOf(string sheetName)
        {
            if (_codecBySheet.TryGetValue(sheetName ?? string.Empty, out var codec))
                return codec;

            if (string.Equals(sheetName, StandaloneSheetName, StringComparison.OrdinalIgnoreCase)
                && !(KeyCodec is FlatKeyCodec))
                return new FlatKeyCodec(DefaultStandaloneKeyColumn);

            return KeyCodec;
        }

        private void BuildGroups()
        {
            if (_sheetOrder.Count > 0)
                return;

            foreach (var target in Targets)
            {
                var sheet = GetSheetName(target);
                if (!_targetsBySheet.TryGetValue(sheet, out var list))
                {
                    list = new List<TableTarget>();
                    _targetsBySheet[sheet] = list;
                    _sheetOrder.Add(sheet);
                }
                list.Add(target);
            }
        }

        /// <summary> 대상의 번역 시트 이름. 지정값이 없으면 규약으로 만든다 </summary>
        public string GetSheetName(TableTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!string.IsNullOrEmpty(target.TranslationSheetName))
                return target.TranslationSheetName;

            if (target.IsStandalone)
                return StandaloneSheetName;

            return string.IsNullOrEmpty(TranslationSheetName)
                ? TranslationSheetPrefix + target.Table
                : TranslationSheetName;
        }

        /// <summary> 대상의 시트 읽기 레이아웃을 만든다. 그 시트를 공유하는 대상이 전부 들어간다 </summary>
        public SheetLayout GetLayout(TableTarget target)
        {
            return GetLayout(GetSheetName(target));
        }

        public SheetLayout GetLayout(string sheetName)
        {
            return new SheetLayout(
                sheetName,
                TargetsOf(sheetName),
                CodecOf(sheetName),
                Languages.EnabledIds,
                DataRowOffset);
        }

        public TableTarget FindTarget(string table)
        {
            foreach (var target in Targets)
            {
                if (string.Equals(target.Table, table ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return target;
            }

            return null;
        }
    }
}
