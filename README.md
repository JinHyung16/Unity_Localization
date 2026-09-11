# Translation
> 기존 저장소 유실로 복구한 코드를 다시 올렸습니다. 커밋 이력은 재업로드 시점부터 시작하고, 실제 작업은 2026년 4월부터입니다.  

게임 안의 모든 글자를 여러 언어로 보여 주기 위한 Unity 번역 SDK.

기획이 관리하는 **게임 DB** 와 번역가가 채우는 **번역 시트** 를 따로 두고, 시트를 언어별 JSON 으로 구워 게임이 읽는다. 게임 DB 를 다시 구워도 번역은 그대로고, 번역 한 줄을 고쳐도 게임 DB 는 건드리지 않는다.

```
 게임 DB (JSON · CSV · 엑셀)        기획이 관리. 원문(한국어)이 여기 있다
        │
        │  Sync        원문을 시트로 옮긴다. 새 키는 붙이고, 바뀐 원문은 표시한다
        ▼
 번역 시트 (CSV · 엑셀 · 구글 시트)  번역가가 언어 열을 채운다
        │
        │  Export      시트를 굽는다
        ▼
 Korean.json · English.json …      게임이 읽는다
 manifest.json                     버전과 언어 목록
 LocalKey.csv                      번들이 오기 전에 쓸 글자
 LocalKey.cs                       키 enum (선택)
        │
        ▼
 게임    LocalizeText 컴포넌트 · Localization.GetString(LocalKey.X) · FieldInjector
```

## 목차

1. [구조 한눈에](#1-구조-한눈에)
2. [설치](#2-설치)
3. [10분 만에 돌려 보기 — 샘플](#3-10분-만에-돌려-보기--샘플)
4. [내 프로젝트에 붙이기](#4-내-프로젝트에-붙이기)
5. [매일 하는 일 — Sync · 번역 · Export](#5-매일-하는-일--sync--번역--export)
6. [게임 코드에서 쓰기](#6-게임-코드에서-쓰기)
7. [설정 항목 전부](#7-설정-항목-전부)
8. [명령줄 — translation.exe](#8-명령줄--translationexe)
9. [자주 묻는 것](#9-자주-묻는-것)
10. [저장소 구조와 배포](#10-저장소-구조와-배포)

---

## 1. 구조 한눈에

### 글자는 두 종류다

| 종류 | 예 | 키 | 시트 |
|---|---|---|---|
| **UI 문자열** | 확인, 취소, "보유 함선 {0}척" | 이름 하나 (`Common_Confirm`) | `LocalizationData.csv` |
| **게임 표의 글자** | ShipData 의 Name · Description | 표 + 컬럼 + Id (`ShipData_Name`, `1`) | `TranslationData.csv` |

UI 문자열은 코드와 프리팹에서 키로 꺼낸다. 게임 표의 글자는 표를 읽은 직후 한 번에 번역을 덮어써서, 게임 코드는 번역이 있는지도 모르게 한다.

### 누가 무엇을 하나

| 사람 | 하는 일 | 만지는 것 |
|---|---|---|
| 기획 | 게임 DB 에 원문을 적는다. UI 문자열도 `LocalizationData` 표에 적는다 | 게임 DB |
| 클라 | Translation Settings 에셋에 폴더와 번역 대상을 적고 **Sync · Export** 버튼을 누른다 | Unity |
| 번역가 | 시트의 언어 열을 채운다 | CSV · 엑셀 · 구글 시트 |
| CI | 같은 일을 `translation.exe` 로 돌린다 | `translation.config.json` |

### 파일이 어디에 놓이나

전부 Translation Settings 에셋에서 고른다. 어디든 된다.

```
Assets/
  GameData/                    ← 게임 DB 폴더 (Game Data Folder)
    LocalizationData.json
    ShipData.json
  Localize/                    ← 번역 작업 폴더. 에셋을 여기 두면 시트 기본 자리가 여기
    TranslationSettings.asset
    LocalizationData.csv
    TranslationData.csv
    translation.config.json    ← exe 용. "설정 파일 내보내기" 버튼이 만든다
  Resources/Localization/      ← 결과 폴더 (Output Folder). 게임이 읽는다
    Korean.json  English.json  manifest.json  LocalKey.csv
  Scripts/Generated/           ← 생성 코드 폴더 (Script Folder). 선택
    LocalKey.cs  LocalizeText.cs
```

---

## 2. 설치

### Unity 패키지

`Window > Package Manager > + > Add package from git URL`

```
https://github.com/JinHyung16/Unity_Localization.git?path=/Packages/com.translation.unity#<태그>
```

- `<태그>` 자리에 [Releases](https://github.com/JinHyung16/Unity_Localization/releases) 의 태그를 넣는다. `?path=` 는 꼭 붙인다.
- Unity 2021.3 이상. API Compatibility Level 은 `.NET Standard 2.1`.
- `LocalizeText` 컴포넌트는 TextMeshPro 를 쓴다. 어드레서블 로더는 `com.unity.addressables` 가 있을 때만 켜진다. 둘 다 없어도 나머지는 돈다.
- git 인증이 번거로우면 [Releases](https://github.com/JinHyung16/Unity_Localization/releases) 의 `Translation.Unity-<태그>.tgz` 를 `Add package from tarball` 로 넣는다.

### translation.exe (선택)

Unity 밖에서 돌릴 때만 필요하다. CI, 엑셀 게임 DB, 구글 시트가 그 경우다. [Releases](https://github.com/JinHyung16/Unity_Localization/releases) 의 `translation-win-x64.zip` 을 풀면 `translation.exe` 하나가 나온다. .NET 설치가 필요 없다.

직접 빌드하려면 .NET 8 SDK 로 `dotnet build Translation.Sdk.sln -c Release`. 결과는 `Tools/Translation.Cli/bin/Release/net8.0/translation.exe`.

---

## 3. 10분 만에 돌려 보기 — 샘플

패키지에 든 샘플이 게임 DB → 시트 → JSON → 화면까지 한 바퀴를 다 담고 있다.

1. Package Manager 에서 Translation 패키지를 열고 **Samples > Basic > Import**.
2. 빈 씬에 빈 오브젝트를 만들고 `SampleBoot` 컴포넌트를 붙인다.
3. Play. 함선 목록과 언어 버튼이 나온다. 버튼을 누르면 화면 글자가 전부 바뀐다.

그 다음 `Assets/Samples/Translation/<버전>/Basic/` 을 열어 본다. 어디를 보면 되는지는 그 안의 `README.md` 에 적혀 있다. 게임 DB 의 JSON 버전과 엑셀 버전, 시트, 결과 JSON, 사용 코드가 전부 있다.

---

## 4. 내 프로젝트에 붙이기

### 4.1 게임 DB 를 준비한다

표 하나 = 파일 하나. UI 문자열도 표 하나로 둔다 (`LocalizationData`).

```json
// GameData/LocalizationData.json      UI 문자열. Key 와 원문
[
  { "Key": "Common_Confirm", "Korean": "확인" },
  { "Key": "Fleet_Count",    "Korean": "보유 함선 {0}척" }
]

// GameData/ShipData.json              게임 표. Name 과 Description 을 번역할 것
[
  { "Id": 1, "Name": "배틀크루저", "Description": "함대의 기본 전열함.", "Power": 1200 }
]
```

| 형식 | 조건 |
|---|---|
| JSON | 루트가 오브젝트 배열이거나, 오브젝트 안에 배열 하나. 숫자는 문자열로 읽고 `null` 은 빈 글자 |
| CSV | 1행이 헤더 |
| 엑셀 | 폴더면 `<표>.xlsx`, 파일 하나면 워크시트 이름이 표. exe 로만 읽는다 |

- 문자열 배열 컬럼(`"Tags": ["a", "b"]`)을 번역하려면 그 대상에 `Array Separator` 를 준다. 없으면 건너뛰고 경고에 남긴다.
- 오브젝트가 든 컬럼은 무시한다. 하위 폴더도 찾는다.

### 4.2 Translation Settings 에셋을 만든다

`Create > Translation > Translation Settings`. 번역 작업 폴더(예: `Assets/Localize`)에 만든다.

| 필드 | 적는 것 |
|---|---|
| `Source Language` | 원문 언어. 게임 DB 글자가 이 언어다 |
| `Languages` | 쓰는 언어. `Id` 가 시트의 열 이름이자 결과 파일 이름 (`Korean`, `English`, 또는 팀이 쓰는 `KR`, `US`). `Bcp47` 은 기기 언어 자동 감지용, `Fallback Id` 는 번역이 없을 때 대신 쓸 언어 |
| `Game Data Folder` | 게임 DB 폴더를 끌어다 놓는다 |
| `Game Data Format` | `Json` 또는 `Csv` |
| `Sheet Folder` | 번역 시트가 놓일 폴더. 비우면 에셋이 있는 폴더 |
| `Output Folder` | 결과 JSON 을 넣을 폴더. 게임이 읽을 자리(`Resources/Localization`, 어드레서블 폴더)를 바로 고른다. 비우면 에셋 옆 `Bundles/` |
| `Targets` | 무엇을 번역하나. 아래 참고 |
| `Script Folder` · `Script Namespace` | `LocalKey.cs` 와 `LocalizeText.cs` 를 넣을 폴더. 비우면 안 만든다 (6.2) |

`Targets` 는 한 줄이 번역 대상 하나다.

| 줄 | `Table` | `Id Column` | `Columns` | 그 밖에 |
|---|---|---|---|---|
| UI 문자열 | 비운다 | `Key` | 비운다 | `Source Table` = `LocalizationData` |
| 게임 표 | `ShipData` | `Id` | `Name`, `Description` | 두 컬럼이 합쳐져야 행이 정해지면 `Sub Id Column` |

### 4.3 Sync → 번역 → Export

인스펙터 아래 버튼을 누른다.

| 버튼 | 하는 일 |
|---|---|
| **Sync** | 게임 DB 원문으로 시트를 최신화한다. 처음이면 시트를 만든다. **Sync (미리 보기)** 는 쓰지 않고 결과만 콘솔에 |
| **Validate** | 시트를 검사한다. 빈 번역, `{0}` 불일치, 중복 키 |
| **Export** | 시트를 `Output Folder` 에 언어별 JSON 으로 굽는다. `Script Folder` 가 있으면 `LocalKey.cs` 도 |
| **설정 파일 내보내기** | 에셋 옆에 `translation.config.json` 과 `sync.bat` · `validate.bat` · `export.bat` 을 쓴다. exe 로 돌릴 때 |

Sync 를 누르면 시트 두 장이 생긴다. 번역가가 언어 열을 채운다. 엑셀로 열어 저장해도 된다.

```
LocalizationData.csv
  LocalKey,Korean,English,_State
  Common_Confirm,확인,,신규
  Fleet_Count,보유 함선 {0}척,,신규

TranslationData.csv              표가 몇 개든 한 장
  TranslationKey,Id,Korean,English,_State
  ShipData_Name,1,배틀크루저,,신규
  ShipData_Description,1,함대의 기본 전열함.,,신규
```

### 4.4 게임에서 읽는다

```csharp
using Translation.Unity;

// 게임 시작 때 한 줄. LocalKey.csv 로 먼저 띄우고 번들로 바꿔 끼운다
TranslationBootstrap.StartFromResources("Localization/LocalKey", "Localization");
```

UI 텍스트에는 `LocalizeText` 컴포넌트를 붙이고 키를 고른다. 코드에서는 `Localization.GetString(LocalKey.Common_Confirm)`. 자세한 것은 6장.

---

## 5. 매일 하는 일 — Sync · 번역 · Export

게임 DB 가 바뀔 때마다 **Sync → 번역 → Validate → Export** 다.

### 5.1 Sync 가 시트에 하는 일

| 상황 | 하는 일 | 시트에 남는 것 |
|---|---|---|
| 게임 DB 에 있는데 시트에 없는 키 | 행을 끝에 붙이고 원문을 채운다 | `_State` = `신규` |
| 그 원문과 똑같은 글자가 이미 번역돼 있으면 | 그 번역을 복사해 넣는다 | 번역 앞에 `[추천데이터로 번역됨]` |
| 시트에 있는데 원문이 바뀐 키 | 원문을 새 글자로 바꾼다 | 기존 번역 앞에 `[번역수정필요]`, `_State` = `원문변경` |
| 시트에 있는데 게임 DB 에서 사라진 키 | 그대로 둔다 | `_State` = `삭제됨` |
| 사라진 키를 지우고 싶으면 | exe 로 `--prune` | 행이 없어진다 (되돌릴 수 없다) |
| 원문이 그대로인 키 | 아무것도 안 한다 | |

- 팀이 둔 다른 컬럼(담당자, 메모, 검수)과 행 순서는 그대로다. 새 행은 끝에 붙는다.
- 언어 컬럼이나 `_State` 컬럼이 없으면 헤더 끝에 만든다.
- 게임 DB 에 파일이 없는 표는 건너뛰고, 그 표의 키를 사라진 것으로 보지 않는다.

Sync 가 끝난 시트:

```
LocalKey          Korean      English                    _State
Common_Confirm    확인        OK
Common_Cancel     취소하기    [번역수정필요]Cancel         원문변경
Common_Close      닫기                                   삭제됨
Popup_Confirm     확인        [추천데이터로 번역됨]OK      신규
```

번역가는 표식을 확인하고 지운다. 남아 있으면 Validate 가 잡고, Export 는 그대로 내보내므로 게임 화면에 표식이 보인다.

### 5.2 Validate 가 잡는 것

| 코드 | 수준 | 뜻 |
|---|---|---|
| `missing-translation` | 경고 (`--strict` 면 오류) | 번역이 비었다 |
| `unfinished-translation` | 경고 (`--strict` 면 오류) | Sync 표식이 남아 있다 |
| `placeholder-mismatch` | 오류 | 원문과 번역의 `{0}` 집합이 다르다. 게임에서 글자가 깨진다 |
| `duplicate-key` | 오류 | 같은 키가 두 번 |
| `empty-source` | 경고 | 원문이 비었다 |

### 5.3 Export 가 만드는 것

```
<Output Folder>/
  manifest.json      버전 + 언어 목록 + 언어별 해시 + 언어별 폰트 주소
  Korean.json        언어별 번들. 빈 번역은 대체 언어 글자가 미리 들어 있어 한 파일만으로 완전하다
  English.json
  LocalKey.csv       UI 문자열 전 언어 한 장. 번들이 오기 전에 쓸 글자
<Script Folder>/
  LocalKey.cs        키 enum + Localization.GetString. Export 마다 다시 쓴다
  LocalizeText.cs    TMP 컴포넌트. 없을 때만 만든다
```

Unity 가 읽는 자리:

| 자리 | 조건 |
|---|---|
| `Assets/Resources/<폴더>` | 파일이 `.json` 이면 TextAsset 으로 들어온다 (기본값) |
| `Assets/StreamingAssets/<폴더>` | 없음 |
| 어드레서블 | 폴더에 라벨 하나 붙이면 하위 파일이 다 들어간다 |

### 5.4 번역 시트를 엑셀이나 구글 시트로

exe 로 돌릴 때 `translation.config.json` 의 `translations` 를 적는다 (7.5). Unity 안 버튼은 CSV 만 된다.

| 종류 | 시트 하나가 무엇인가 |
|---|---|
| CSV | 폴더 안의 `<시트명>.csv` |
| 엑셀 (폴더) | 폴더 안의 `<시트명>.xlsx` 의 첫 워크시트. 하위 폴더도 찾는다 |
| 엑셀 (파일 하나) | `.xlsx` 안의 `<시트명>` 워크시트. 워크시트 이름은 31자까지 |
| 구글 시트 | 스프레드시트 안의 `<시트명>` 탭 |

시트 공통 규칙: 1행이 헤더, 컬럼 순서는 상관없고 이름으로 찾는다(대소문자 무시). SDK 가 모르는 컬럼은 건드리지 않는다. 2행이 타입 행(`int!`, `string`)이면 `Data Row Offset` 을 1로. 빈 줄은 건너뛰고, 키가 빈 행은 경고에 남고 무시된다.

---

## 6. 게임 코드에서 쓰기

네임스페이스 `Translation.Unity` (부팅 · 로더 · 런타임), `Translation` (코어). 생성 코드(`LocalKey`, `Localization`, `LocalizeText`)는 `Script Namespace` 에 적은 네임스페이스에 생긴다.

### 6.1 게임 시작 때

```csharp
// 둘 다 Resources 에 있을 때. 동기라 바로 결과가 온다
bool ok = TranslationBootstrap.StartFromResources("Localization/LocalKey", "Localization");

// 번들이 StreamingAssets 에 있을 때
TranslationBootstrap.StartFromStreamingAssets("Localization/LocalKey", "Localization", ok => { });

// 번들이 어드레서블이나 서버에서 올 때. 두 번째 인자에 번들 읽는 법을 준다
TranslationBootstrap.Start("Localization/LocalKey",
    next => TranslationAddressablesLoader.FromAddressables("localization", next),
    ok => { });
```

첫 인자는 `Resources` 안의 `LocalKey.csv` 자리다. 이걸로 먼저 글자를 띄워 두고, 번들이 읽히면 바꿔 끼운다. 번들이 못 와도 CSV 글자로 계속 간다. 첫 인자를 비우면 CSV 단계를 건너뛴다.

로더만 따로 부를 수도 있다.

```csharp
TranslationLoader.FromResources("Localization");
TranslationLoader.FromStreamingAssets("Localization", ok => { });
TranslationAddressablesLoader.FromAddressables("localization", ok => { });
TranslationRuntime.Load(languageBundleBytes, manifestBytes);      // 바이트를 직접 구했을 때
```

로더는 매니페스트를 읽고, 쓸 언어를 정하고(6.4), 그 언어 파일 하나만 읽는다.

### 6.2 UI 텍스트 — `LocalizeText` 와 `LocalKey`

Translation Settings 의 `Script Folder` 를 정하고 Export 하면 두 파일이 생긴다.

| 파일 | 내용 | 다시 쓰나 |
|---|---|---|
| `LocalKey.cs` | UI 문자열 시트의 키 전부가 든 `enum LocalKey`, 키 이름을 오가는 `LocalKeys`, 글자를 꺼내는 `Localization` | Export 마다 다시 쓴다. 손으로 고치지 않는다 |
| `LocalizeText.cs` | TMP 텍스트에 붙이는 컴포넌트. 키를 고르면 지금 언어의 글자를 넣고, 언어가 바뀌면 다시 넣는다 | 없을 때만 만든다. 프로젝트에 맞게 고쳐도 된다 |

프리팹의 TMP 텍스트에 `Add Component > Translation > Localize Text` 를 붙이고 `Key` 를 고른다. 드롭다운은 검색이 된다. `None` 이면 아무것도 하지 않아 손으로 적은 글자가 남는다.

```csharp
Localization.GetString(LocalKey.Common_Confirm);              // "확인"
Localization.GetString(LocalKey.Fleet_Count, 3);              // "보유 함선 3척"
Localization.GetString("Common_Confirm");                     // 문자열 키도 된다
Localization.GetStringByEnum(ShipGrade.Rare);                 // "Enum_Rare" 키
Localization.TryGetString(LocalKey.Maybe, out var text);

localizeText.SetKey(LocalKey.Fleet_Count, 3);                 // 코드에서 키를 바꿀 때
localizeText.SetArgs(5);                                      // {0} 값만 바꿀 때
```

- enum 값은 키 이름의 해시라 시트에 키를 끼워 넣거나 순서를 바꿔도 프리팹에 저장된 값이 안 바뀐다. 키 이름을 바꾸면 그 키를 쓰던 자리는 새로 골라야 한다.
- 키는 영문 · 숫자 · `_` 만 되고 숫자로 시작할 수 없다. 안 맞는 키는 enum 에서 빠지고 Export 경고에 남는다. `None` 이라는 키는 쓸 수 없다.
- 다른 enum 필드에도 `[SearchableEnum]` 을 붙이면 같은 검색 드롭다운으로 그려진다.

enum 없이 쓰려면 `TranslationRuntime` 을 바로 부른다.

```csharp
TranslationRuntime.Get("Common_Confirm");
TranslationRuntime.Format("Fleet_Count", 3);
TranslationRuntime.TryGet("Maybe", out var text);
```

### 6.3 게임 표 — `FieldInjector`

표를 읽은 직후 한 번 부른다. 행 객체의 번역 대상 필드를 지금 언어 값으로 덮어쓴다. 번역이 없는 행은 원문이 남는다.

```csharp
var target = new TableTarget("ShipData", "Name", "Description") { IdColumn = "Id" };
var report = new FieldInjector(TranslationRuntime.Catalog).Inject(target, shipRows);   // shipRows: List<Ship>
```

- 행은 class 여야 한다. struct 는 복사본에 써져서 반영되지 않는다.
- 필드나 프로퍼티 이름이 컬럼 이름과 같아야 한다 (대소문자 무시). `string`, `string[]`, `List<string>` 이 된다.
- 언어가 바뀌면 표를 다시 읽고 다시 부른다. 샘플의 `ShipTable.cs` 가 그 꼴이다.

표 하나 값만 꺼내려면 `TranslationRuntime.Get("ShipData", "Name", "1")`. 매 프레임 부르면 키 문자열을 매번 만드니 한 번 꺼내 두는 편이 낫다.

### 6.4 언어

로더가 언어를 정하는 순서: ① `SetLanguage` 로 저장한 값 ② `Resources/TranslateOption.asset` 의 기기 언어 짝 ③ 매니페스트 `bcp47` 로 기기 언어 매칭 ④ 원문 언어.

```csharp
TranslationRuntime.SetLanguage("English", ok => { });          // 저장하고, 같은 곳에서 다시 읽고, 화면이 갱신된다
TranslationRuntime.OnLanguageChanged += id => { };             // LocalizeText 는 알아서 다시 그린다. 코드로 넣은 글자만 직접
TranslationRuntime.LanguageId;                                  // 지금 언어
TranslationRuntime.Manifest.Languages;                          // 언어 선택 UI 에 띄울 목록. DisplayName 이 있다
TranslationRuntime.BundleVersion;                               // 어느 패치가 적용됐는지
```

`SetLanguage` 는 마지막 로더가 읽은 곳에서 다시 읽는다. 바이트를 직접 `Load` 했으면 `RegisterReload` 로 다시 읽는 법을 알려 준다.

`TranslateOption` 은 `Create > Translation > Translate Option` 으로 `Resources/TranslateOption.asset` 을 만들어 쓴다. Unity 의 `SystemLanguage` 와 시트 열 이름을 짝짓고, `IsApply` 로 미출시 언어를 끈다. 없어도 된다.

### 6.5 없는 키

```csharp
TranslationRuntime.MissingKeyPolicy = MissingKeyPolicy.ReturnMarkedKey;  // 기본. "@키이름" 으로 나와 눈에 띈다
TranslationRuntime.MissingKeyPolicy = MissingKeyPolicy.ReturnKey;        // 키 이름 그대로
TranslationRuntime.MissingKeyPolicy = MissingKeyPolicy.ReturnEmpty;      // 빈 글자
TranslationRuntime.LogMissingKeys = true;                                 // 없는 키마다 경고 로그. QA 빌드에서 켠다
```

### 6.6 폰트

SDK 는 폰트를 로드하지 않는다. 언어 설정의 `Font` 문자열을 그대로 실어 나르고, 없으면 대체 언어 것을 준다.

```csharp
var address = TranslationRuntime.FontAddress;   // "Fonts/NotoSansKR" 또는 null
```

### 6.7 서버에서 내려받는 번들 — `TranslationBundleCache`

다운로드는 프로젝트가 하고, SDK 는 보관과 "무엇을 받아야 하나" 판정을 한다.

```csharp
var cache = new TranslationBundleCache();                 // persistentDataPath/translation
var remote = BundleReader.ReadManifest(manifestBytes);
foreach (var id in cache.Compare(remote).LanguagesToFetch)
{
    var info = remote.Find(id);
    cache.Save(info.File, await Download(info.File), info.Hash);   // 해시가 다르면 예외
}
cache.SaveManifest(manifestBytes);

var languageId = TranslationRuntime.ResolveLanguage(remote);
TranslationRuntime.Load(cache.Load(remote.Find(languageId).File), manifestBytes);
```

---

## 7. 설정 항목 전부

Unity 에셋과 `translation.config.json` 은 같은 내용이다. 에셋의 **설정 파일 내보내기** 가 이 파일을 쓴다. 상대 경로는 이 파일이 있는 폴더 기준이다.

```json
{
  "sourceLanguage": "Korean",
  "languages": [
    { "id": "Korean",  "displayName": "한국어",  "bcp47": "ko" },
    { "id": "English", "displayName": "English", "bcp47": "en", "fallbackId": "Korean" }
  ],
  "gameData": { "type": "json", "path": "../Assets/GameData" },
  "output":   { "directory": "../Assets/Resources/Localization" },
  "script":   { "directory": "../Assets/Scripts/Generated", "namespace": "Game" },
  "targets": [
    { "table": "",          "sourceTable": "LocalizationData", "idColumn": "Key" },
    { "table": "ShipData",  "idColumn": "Id", "columns": ["Name", "Description"] },
    { "table": "SkillData", "idColumn": "Id", "columns": ["Name"] }
  ]
}
```

### 7.1 언어 — `sourceLanguage` · `languages`

| 키 | 필수 | 뜻 |
|---|---|---|
| `sourceLanguage` | ● | 원문 언어. 게임 DB 글자가 이 언어다. 모든 언어의 마지막 대체 언어 |
| `id` | ● | 시트 열 이름 = 결과 파일 이름 |
| `displayName` | | 언어 선택 UI 에 띄울 이름. 비우면 `id` |
| `bcp47` | | 기기 언어 자동 감지에만 쓴다 (`ko`, `en`, `zh-Hant`) |
| `fallbackId` | | 번역이 없을 때 대신 쓸 언어. 비우면 `sourceLanguage` |
| `font` | | 이 언어가 쓸 폰트 주소. 문자열을 그대로 매니페스트에 싣는다 |
| `enabled` | | `false` 면 Sync · Validate · Export 전부에서 뺀다 |

### 7.2 번역할 것 — `targets`

```json
"targets": [
  { "table": "",          "sourceTable": "LocalizationData", "idColumn": "Key", "sourceColumn": "Korean" },
  { "table": "ShipData",  "idColumn": "Id", "columns": ["Name", "Description"] },
  { "table": "StageData", "idColumn": "StageKey", "subIdColumn": "PhaseIndex", "columns": ["Title"] },
  { "table": "TipData",   "idColumn": "Id", "columns": ["Lines"], "arraySeparator": "|" },
  { "table": "ItemData",  "idColumn": "Id", "columns": ["Name"], "sheet": "ItemSheet" }
]
```

| 키 | 뜻 |
|---|---|
| `table` | 게임 DB 표 이름. **비우면 UI 문자열 세트** (하나만) |
| `idColumn` | 행 식별자 컬럼 (기본 `id`). UI 세트에서는 키 이름이 든 컬럼 |
| `columns` | 번역할 컬럼. 게임 표에서 필수 |
| `subIdColumn` | 보조 식별자. 두 컬럼이 합쳐져야 한 행이 정해지는 표에서만 |
| `arraySeparator` | 문자열 배열 컬럼을 한 셀에 이을 구분자 |
| `sourceTable` | Sync 가 원문을 읽을 표. 비우면 `table`. **UI 세트는 이걸 줘야 Sync 가 돈다** |
| `sourceColumn` | UI 세트에서 원문이 든 컬럼. 비우면 `sourceLanguage` 와 같은 이름 |
| `sheet` | 이 대상이 든 시트 이름. 비우면 UI 세트는 `LocalizationData`, 표는 `TranslationData` |

### 7.3 게임 DB — `gameData`

```json
"gameData": { "type": "json",  "path": "../Assets/GameData" }
"gameData": { "type": "csv",   "path": "../gamedata/csv", "dataRowOffset": 1 }
"gameData": { "type": "excel", "path": "../gamedata.xlsx" }
```

Sync 를 안 쓰면 없어도 된다.

### 7.4 결과 — `output`

| 키 | 기본값 | 뜻 |
|---|---|---|
| `directory` | `./Bundles` | 결과 폴더. exe 의 `--out` 이 덮어쓴다 |
| `fileExtension` | `.json` | 언어 파일 확장자. Unity 가 TextAsset 으로 읽으려면 `.json` |
| `csvFileName` | `LocalKey.csv` | UI 문자열 전 언어 한 장. `""` 면 안 만든다 |
| `manifestFileName` | `manifest.json` | |
| `bakeFallback` | `true` | 빈 번역에 대체 언어 글자를 미리 넣는다 |
| `compression` | `none` | `none` · `gzip` |
| `indentJson` | `false` | 사람이 읽기 좋게 들여쓴다 |

### 7.5 번역 시트 위치 — `translations`

CSV 를 설정 파일 옆에 둘 거면 안 적는다.

```json
"translations": { "type": "csv",   "path": "./Sheets" }
"translations": { "type": "excel", "path": "./Sheets" }          // 폴더
"translations": { "type": "excel", "path": "./Sheets.xlsx" }     // 파일 하나
"translations": { "type": "googleSheets", "spreadsheetId": "https://docs.google.com/spreadsheets/d/…", "credentials": "./secrets/sa.json" }
```

구글 시트:

- `spreadsheetId` 에 URL 을 그대로 넣어도 된다.
- `credentials` 는 서비스 계정 키 JSON 이거나 OAuth 클라이언트 JSON. 서비스 계정이면 브라우저 없이 돈다.
- Validate · Export 는 읽기 권한만, **Sync 만 쓰기 권한**을 쓴다. 서비스 계정으로 Sync 하려면 시트에 편집자로 공유한다.
- `secrets/` 와 `*service-account*.json` 은 `.gitignore` 에 있다.

### 7.6 코드 생성 — `script`

| 키 | 뜻 |
|---|---|
| `directory` | `LocalKey.cs` 와 `LocalizeText.cs` 를 넣을 폴더 |
| `namespace` | 생성 코드의 네임스페이스. 비우면 `Game` |

없으면 안 만든다.

### 7.7 시트 이름과 키 — `naming` · `keyCodec` · `keyCodecs`

기본값으로 충분하면 안 적는다.

| 키 | 기본값 | 뜻 |
|---|---|---|
| `naming.standaloneSheetName` | `LocalizationData` | UI 문자열 시트 이름 |
| `naming.translationSheetName` | `TranslationData` | 표 번역이 전부 들어가는 시트. `""` 로 비우면 표마다 `translationSheetPrefix` + 표 이름 시트를 쓴다 |
| `naming.translationSheetPrefix` | `Localization_` | 위를 비웠을 때만 쓴다 |
| `naming.dataRowOffset` | `0` | 헤더 다음에 건너뛸 행 수 |

키를 시트에 담는 방법은 세 가지다. 기본은 표 시트가 ②, UI 시트가 ①이다.

**① `flat` — 키가 한 컬럼.** UI 시트의 `LocalKey`. 표를 담을 때는 `표_컬럼_Id` 로 이어 붙인다.

```json
"keyCodec": { "kind": "flat", "keyColumn": "LocalKey", "separator": "_" }
```

**② `translationKey` — `표_컬럼` + `Id` 두 컬럼.** 표가 몇 개든 시트 한 장.

```json
"keyCodec": { "kind": "translationKey", "keyColumn": "TranslationKey", "idColumn": "Id", "separator": "_" }
```

`TranslationKey` 는 `targets` 와 글자 그대로 대조한다. 표 이름에 `_` 가 있어도 안 깨지고, `targets` 에 없는 값(오타)은 경고에 남고 버려진다.

**③ `tuple` — `Table` · `Column` · `Id` · `SubId` 네 컬럼.** 이미 이 꼴로 쓰던 팀용.

```json
"keyCodec": { "kind": "tuple", "tableColumn": "Table", "columnColumn": "Column", "idColumn": "FirstIndex", "subIdColumn": "SecondIndex" }
```

시트마다 다르게 하려면 `keyCodecs` 에 `{ 시트명: 코덱 }` 을 적는다.

### 7.8 Sync 표식 — `sync`

```json
"sync": {
  "changedMarker":   "[번역수정필요]",
  "suggestedMarker": "[추천데이터로 번역됨]",
  "stateColumn":     "_State",
  "stateNew":        "신규",
  "stateChanged":    "원문변경",
  "stateRemoved":    "삭제됨"
}
```

전부 선택이고 위 값이 기본값이다. `stateColumn` 을 `""` 로 두면 상태 컬럼을 안 만든다.

---

## 8. 명령줄 — translation.exe

번역 작업 폴더에 `translation.exe` 를 넣는다. Unity 에셋의 **설정 파일 내보내기** 를 눌렀으면 설정 파일과 bat 이 이미 있고, 아니면 `translation init` 이 만든다.

```
Localize/
  translation.exe
  translation.config.json    ← 설정 (7장)
  sync.bat                   ← 더블클릭: 게임 DB → 시트
  validate.bat               ← 더블클릭: 시트 검사
  export.bat                 ← 더블클릭: 시트 → JSON
```

bat 은 아래 명령을 `--verbose` 로 부른 것이다.

```
translation init                          설정 파일과 bat 세 개를 만든다
translation sync [--prune] [--dry-run]    게임 DB → 번역 시트. --dry-run 은 결과만 보여 준다
translation validate [--strict]           번역 시트 검사
translation export -v <버전> [-o <폴더>]  번역 시트 → 언어별 JSON. -v 를 비우면 UTC 시각
translation diff <이전 manifest> <이후 manifest>   어느 언어가 바뀌었나
```

| 옵션 | 뜻 |
|---|---|
| `-c, --config <경로>` | 설정 파일 (기본 `translation.config.json`) |
| `--verbose` | 경고와 문제를 전부 출력 (기본은 20건까지) |
| `--strict` | 검증 오류가 있으면 종료 코드 3. Validate 에서는 미번역과 표식 남은 번역도 오류로 본다. Export 는 파일을 안 쓴다 |

종료 코드: `0` 성공 · `1` 사용법 오류 · `2` 실패 · `3` 검증 오류. CI 에서는 `validate --strict` 와 `export --strict` 를 쓴다.

---

## 9. 자주 묻는 것

**화면에 `@Common_Confirm` 처럼 나온다.** 그 키가 번들에 없다. 시트에 키가 있는지, Export 를 다시 했는지, 결과 폴더가 게임이 읽는 자리인지 본다. `LogMissingKeys = true` 로 켜면 없는 키가 로그에 남는다.

**화면에 `[번역수정필요]` 가 보인다.** 번역가가 표식을 안 지웠다. Validate 가 `unfinished-translation` 으로 잡는다.

**Sync 했더니 시트에 `삭제됨` 이 잔뜩 생겼다.** 게임 DB 폴더가 잘못됐거나 표 파일 이름이 바뀌었다. 표 파일이 아예 없으면 건너뛰지만, 있는데 행이 비었으면 전부 사라진 것으로 본다. `Sync (미리 보기)` 로 먼저 본다.

**언어를 바꿨는데 글자가 안 바뀐다.** `LocalizeText` 가 붙은 글자는 알아서 바뀐다. 코드로 `Get` 해서 넣은 글자는 `OnLanguageChanged` 에서 다시 넣어야 한다. 게임 표는 `FieldInjector` 를 다시 부른다.

**enum 에 새 키가 안 보인다.** Export 를 안 했다. `LocalKey.cs` 는 Export 때 다시 써진다.

**프리팹의 Key 가 `(없는 값: 123)` 으로 나온다.** 그 키가 시트에서 없어졌거나 이름이 바뀌었다. 새로 고른다.

**게임 표 번역이 안 들어간다.** 행이 struct 가 아닌지, 필드 이름이 컬럼 이름과 같은지, `Inject` 를 `TranslationRuntime` 이 읽힌 뒤에 불렀는지 본다. `report.Warnings` 에 이유가 있다.

**번들이 크다.** `output.compression` 을 `gzip` 으로. 읽을 때는 자동으로 푼다. Unity `Resources` 에 두려면 확장자는 그대로 `.json` 이어야 하니 `fileExtension` 을 같이 둔다.

---

## 10. 저장소 구조와 배포

```
Packages/com.translation.unity/      Unity 패키지
  Runtime/Core/                      Unity 의존 없는 코어. exe 와 Unity 가 같이 쓴다
    Sync/  Bundle/  Validate/  Csv/  Config/  Keys/  Inject/
  Runtime/                           부팅 · 로더 · 런타임 · 번들 보관
  Runtime/Addressables/              어드레서블 로더 (있을 때만 켜진다)
  Editor/                            Translation Settings 에셋과 버튼, 검색 드롭다운
  Samples~/Basic/                    샘플 (3장)
Tools/
  Translation.Cli/                   translation.exe
  Translation.Sources.Excel/         엑셀 (ClosedXML)
  Translation.Sources.GoogleSheets/  구글 시트 (Google.Apis.Sheets.v4)
Sandbox/                             패키지를 물려 둔 빈 Unity 프로젝트 (확인용)
.github/workflows/release.yml        태그 → Release
```

태그를 올리면 GitHub Actions 가 Release 에 `translation-win-x64.zip` 과 `Translation.Unity-<태그>.tgz` 를 붙인다.

```bash
git tag <태그>
git push origin <태그>
```
