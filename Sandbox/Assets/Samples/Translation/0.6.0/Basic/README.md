# Basic 샘플

게임 DB → 번역 CSV → 언어별 JSON → 화면까지 한 바퀴가 다 들어 있다. 파일을 하나씩 열어 보면 이 SDK 가 어떻게 도는지 알 수 있고, 빈 씬에서 바로 돌려 볼 수도 있다.

## 돌려 보기

1. Package Manager 에서 Translation 패키지를 열고 **Samples > Basic > Import**. `Assets/Samples/Translation/<버전>/Basic/` 에 복사된다.
2. 빈 씬을 만들고, 빈 오브젝트에 `SampleBoot` 컴포넌트를 붙인다.
3. Play. 함선 목록과 언어 버튼이 나온다. 버튼을 누르면 화면 글자가 전부 바뀐다.

TextMeshPro 가 필요하다. 없으면 `Window > TextMeshPro > Import TMP Essential Resources`.

## 안에 든 것

```
Basic/
  TranslationSettings.asset            ← Unity 안에서 sync · export 를 돌리는 설정. 클릭해서 인스펙터를 보자
  GameData.xlsx                        ← 게임 DB 의 엑셀 버전. 아래 JSON 과 같은 내용 (exe 로 돌릴 때 쓴다)
  Resources/TranslationSample/
    GameData/                          ← 게임 DB (JSON). 표 하나 = 파일 하나
      LocalizationData.json            ← UI 문자열 원문. Key + Korean
      ShipData.json                    ← 게임 표. Name · Description 이 번역 대상
      SkillData.json                   ← 게임 표. Name 이 번역 대상
    Bundles/                           ← export 결과. 게임이 읽는 것
      Korean.json · English.json · Japanese.json
      manifest.json                    ← 버전과 언어 목록
      LocalKey.csv                     ← UI 문자열 전 언어 한 장. 번들이 오기 전에 쓸 글자
  Localize/                            ← 번역 작업 폴더
    LocalizationData.csv               ← UI 문자열 번역 시트 (sync 가 만들고 번역가가 채운다)
    TranslationData.csv                ← 게임 표 번역 시트
    translation.config.json            ← exe 용 설정 (게임 DB = JSON)
    translation.excel.config.json      ← 같은 설정, 게임 DB 만 엑셀
  Scripts/
    SampleBoot.cs                      ← 시작점. 부팅 → 화면 만들기 → 언어 바꾸기
    ShipTable.cs                       ← 게임 표를 읽고 FieldInjector 로 번역을 넣는 자리
    LocalKey.cs                        ← export 가 만든 enum + Localization.GetString (손으로 고치지 않는다)
    LocalizeText.cs                    ← export 가 처음 한 번 만든 TMP 컴포넌트 (고쳐 써도 된다)
```

## 어디를 보면 되나

| 알고 싶은 것 | 파일 |
|---|---|
| 게임 시작 때 뭘 부르나 | `SampleBoot.cs` 의 `Start()`. `TranslationBootstrap.StartFromResources` 한 줄 |
| UI 텍스트에 번역을 어떻게 붙이나 | `SampleBoot.cs` 의 `LocalizedLabel`. 실제 프로젝트에서는 프리팹의 TMP 에 `LocalizeText` 를 붙이고 인스펙터에서 키를 고른다 |
| 코드에서 글자를 어떻게 꺼내나 | `SampleBoot.cs` 의 `RefreshDynamicText`. `Localization.GetString(LocalKey.Fleet_Count, 3)` |
| 게임 표(ShipData)의 Name 은 어떻게 번역되나 | `ShipTable.cs`. 표를 읽은 직후 `FieldInjector.Inject` 한 번 |
| 언어를 바꾸면 어떻게 되나 | `SampleBoot.cs` 의 언어 버튼. `TranslationRuntime.SetLanguage(id)` 하나로 저장 + 다시 읽기 + 화면 갱신 |
| 시트 모양 | `Localize/*.csv`. 엑셀로 열어도 된다 |
| 번역이 비면 어떻게 되나 | `TranslationData.csv` 의 `ShipData_Description` 3번 일본어가 비어 있다. `Japanese.json` 에는 영어(대체 언어)가 들어 있다 |

## 시트를 고쳐서 다시 굽기

Unity 안에서: `TranslationSettings.asset` 을 클릭하고 인스펙터 아래 **Sync → Validate → Export**. 결과는 `Resources/TranslationSample/Bundles/` 와 `Scripts/LocalKey.cs` 에 다시 써진다.

exe 로: `Localize/` 에 `translation.exe` 를 넣고 그 폴더에서.

```
translation sync
translation export -v sample-2
translation sync -c translation.excel.config.json     ← 게임 DB 를 엑셀로 읽을 때
```

`GameData/ShipData.json` 에 함선을 하나 추가하고 Sync 를 눌러 보면, 시트에 `신규` 행이 붙는 것이 보인다. 영어를 채우고 Export 하면 화면에 나온다.
