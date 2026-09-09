using System.Collections.Generic;
using TMPro;
using Translation.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace TranslationSample
{
    /// <summary>
    /// 빈 씬의 빈 오브젝트에 붙이고 Play 를 누르면 화면을 코드로 만들어 보여 준다.
    /// 씬 파일 없이도 돌게 하려고 UI 를 코드로 만들었다. 실제 프로젝트에서는 프리팹의 TMP 텍스트에 LocalizeText 를 붙이면 된다.
    ///
    /// 보여 주는 것
    ///   1. TranslationBootstrap  — 게임 시작 때 한 줄. LocalKey.csv 로 먼저 띄우고 번들로 바꿔 끼운다
    ///   2. LocalizeText          — 키를 고르면 글자가 들어가고, 언어가 바뀌면 알아서 다시 그려진다
    ///   3. Localization.GetString — 코드에서 enum 으로 꺼낸다. {0} 자리도 채운다
    ///   4. ShipTable             — 게임 표(ShipData.json)의 Name · Description 을 지금 언어로 바꿔 넣는다
    ///   5. SetLanguage           — 버튼으로 언어를 바꾼다. 저장돼서 다음 실행에도 유지된다
    /// </summary>
    public class SampleBoot : MonoBehaviour
    {
        // Resources/TranslationSample/ 아래 자리. 샘플이 Assets 어디에 있어도 Resources 는 이름으로 찾는다
        private const string CsvPath = "TranslationSample/Bundles/LocalKey";
        private const string BundleDirectory = "TranslationSample/Bundles";

        private TMP_Text _fleetCount;
        private TMP_Text _shipList;
        private static TMP_FontAsset _font;

        private void Start()
        {
            // TMP 기본 폰트에는 한글 · 일본어 글자가 없다. 샘플이라 OS 폰트로 만들어 쓴다.
            // 실제 프로젝트는 폰트 에셋을 두고, 언어별 폰트는 TranslationRuntime.FontAddress 로 고른다
            _font = CreateFontFromOs();

            // 1. 글자 준비. CSV → 번들 순서로 읽고, 어느 쪽이든 읽히면 true
            var ok = TranslationBootstrap.StartFromResources(CsvPath, BundleDirectory);
            Debug.Log("[Sample] 번역 준비 " + (ok ? "완료" : "실패") + " · 언어 " + TranslationRuntime.LanguageId
                      + " · 번들 " + TranslationRuntime.BundleVersion);

            BuildScreen();

            // 언어가 바뀌면 코드로 넣은 글자는 직접 다시 넣는다 (LocalizeText 가 붙은 것은 알아서 된다)
            TranslationRuntime.OnLanguageChanged += OnLanguageChanged;
            RefreshDynamicText();
        }

        private void OnDestroy()
        {
            TranslationRuntime.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(string languageId)
        {
            RefreshDynamicText();
        }

        private void BuildScreen()
        {
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);

            if (UnityEngine.EventSystems.EventSystem.current == null)
                CreateEventSystem();

            // 글자가 잘 보이게 어두운 판을 깐다
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

            var column = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(canvas.transform, false);
            var rect = column.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.95f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            // 2. LocalizeText — 키만 고르면 끝. 언어가 바뀌면 알아서 다시 그려진다
            LocalizedLabel(column.transform, LocalKey.Title_Fleet, 40);

            // 3. 코드에서 꺼내기 — {0} 자리를 채우는 것은 GetString(key, args)
            _fleetCount = Label(column.transform, string.Empty, 24);

            // 4. 게임 표 — ShipData.json 을 읽고 FieldInjector 로 Name · Description 을 지금 언어로 바꾼다
            _shipList = Label(column.transform, string.Empty, 22);
            _shipList.GetComponent<LayoutElement>().flexibleHeight = 1;

            // 5. 언어 버튼 — 매니페스트에 있는 언어를 전부 보여 준다
            LocalizedLabel(column.transform, LocalKey.Title_Language, 28);
            var row = new GameObject("Languages", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(column.transform, false);
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var manifest = TranslationRuntime.Manifest;
            if (manifest != null)
            {
                foreach (var language in manifest.Languages)
                {
                    var id = language.Id;
                    Button(row.transform, language.DisplayName ?? id, () =>
                        TranslationRuntime.SetLanguage(id, changed =>
                            Debug.Log("[Sample] 언어 " + id + (changed ? " 적용" : " 저장만 됨"))));
                }
            }

            // 확인 · 취소 — 흔한 공용 버튼도 키 하나
            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(column.transform, false);
            buttons.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            LocalizedButton(buttons.transform, LocalKey.Common_Confirm);
            LocalizedButton(buttons.transform, LocalKey.Common_Cancel);
        }

        private void RefreshDynamicText()
        {
            var ships = ShipTable.Load();

            _fleetCount.text = Localization.GetString(LocalKey.Fleet_Count, ships.Count);

            var lines = new List<string>(ships.Count);
            foreach (var ship in ships)
            {
                // Grade 같은 enum 값은 "Enum_값이름" 키로 꺼낸다 (Enum_Rare, Enum_Epic)
                lines.Add(ship.Name + "  [" + Localization.GetStringByEnum(ship.Grade) + "]  "
                          + Localization.GetString(LocalKey.Ship_Power, ship.Power) + "\n    " + ship.Description);
            }

            _shipList.text = string.Join("\n", lines);
        }

        /// <summary> 한글 · 일본어가 있는 OS 폰트를 찾아 TMP 폰트 에셋을 만든다. 못 찾으면 null (기본 폰트, 네모로 보인다) </summary>
        private static TMP_FontAsset CreateFontFromOs()
        {
            // 앞에 있는 것부터 찾는다. 이름은 OS 와 언어 설정에 따라 조금씩 달라서 부분 일치로 본다
            var candidates = new[]
            {
                "malgun gothic", "맑은 고딕", "apple sd gothic", "noto sans cjk", "noto sans kr", "nanumgothic", "nanum gothic",
                "yu gothic", "meiryo", "hiragino", "ms gothic", "microsoft yahei", "gulim", "굴림",
            };

            var installed = Font.GetOSInstalledFontNames();

            // 첫 번째로 찾은 것이 주 폰트, 나머지는 대체 폰트. 한 폰트에 없는 글자(일본어 가나 등)는 대체 폰트에서 찾는다
            TMP_FontAsset primary = null;
            var picked = new List<string>();
            foreach (var wanted in candidates)
            {
                string name = null;
                foreach (var candidate in installed)
                {
                    if (candidate.ToLowerInvariant().Contains(wanted))
                    {
                        name = candidate;
                        break;
                    }
                }

                if (name == null || picked.Contains(name))
                    continue;

                try
                {
                    var asset = CreateFontAsset(name);
                    if (asset == null)
                        continue;

                    if (primary == null)
                    {
                        primary = asset;
                        if (primary.fallbackFontAssetTable == null)
                            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    }
                    else
                    {
                        primary.fallbackFontAssetTable.Add(asset);
                    }

                    picked.Add(name);
                    if (picked.Count >= 3)
                        break;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Sample] OS 폰트 " + name + " 을 못 썼다: " + e.Message);
                }
            }

            if (primary == null)
            {
                Debug.LogWarning("[Sample] 한글 · 일본어 글자가 든 OS 폰트를 못 찾아 기본 폰트를 쓴다. 화면에 네모로 보일 수 있다.");
                return null;
            }

            Debug.Log("[Sample] OS 폰트로 TMP 폰트를 만들었다: " + string.Join(" → ", picked));
            return primary;
        }

        /// <summary>
        /// Unity 2023.2 이상(TMP 3.2+)은 폰트 이름으로 바로 만들 수 있다. 그 전 버전은 Font 객체를 거치는데
        /// OS 폰트는 잘 안 읽혀 실패할 수 있다. 그때는 프로젝트에 폰트 파일을 넣고 폰트 에셋을 만든다
        /// </summary>
        private static TMP_FontAsset CreateFontAsset(string familyName)
        {
            var byName = typeof(TMP_FontAsset).GetMethod("CreateFontAsset",
                new[] { typeof(string), typeof(string), typeof(int) });
            if (byName != null)
            {
                foreach (var style in new[] { "Regular", "Normal", "" })
                {
                    var asset = byName.Invoke(null, new object[] { familyName, style, 64 }) as TMP_FontAsset;
                    if (asset != null)
                        return asset;
                }

                return null;
            }

            var font = Font.CreateDynamicFontFromOSFont(familyName, 32);
            return TMP_FontAsset.CreateFontAsset(font, 64, 6,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic);
        }

        /// <summary> 버튼이 눌리려면 EventSystem 이 있어야 한다. Input System 패키지만 켠 프로젝트면 그쪽 모듈을 붙인다 </summary>
        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var module = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
                go.AddComponent(module);
            else
                Debug.LogWarning("[Sample] Input System 의 UI 모듈을 찾지 못해 버튼이 안 눌릴 수 있다. 씬에 EventSystem 을 직접 두면 된다.");
#else
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        private static TMP_Text Label(Transform parent, string text, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (_font != null)
                tmp.font = _font;

            tmp.text = text;
            tmp.fontSize = size;
            go.GetComponent<LayoutElement>().minHeight = size * 1.4f;
            return tmp;
        }

        private static LocalizeText LocalizedLabel(Transform parent, LocalKey key, int size)
        {
            var tmp = Label(parent, string.Empty, size);
            var localize = tmp.gameObject.AddComponent<LocalizeText>();
            localize.SetKey(key);
            return localize;
        }

        private static Button Button(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f);
            go.GetComponent<LayoutElement>().minWidth = 160;
            go.GetComponent<LayoutElement>().minHeight = 44;

            var label = Label(go.transform, text, 22);
            label.alignment = TextAlignmentOptions.Center;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        private static void LocalizedButton(Transform parent, LocalKey key)
        {
            var button = Button(parent, string.Empty, () => Debug.Log("[Sample] " + Localization.GetString(key)));
            button.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizeText>().SetKey(key);
        }
    }
}
