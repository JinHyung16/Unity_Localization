namespace Translation.Cli
{
    internal static class Templates
    {
        public const string Usage = @"Translation — 게임 DB 의 원문으로 번역 시트를 최신화하고, 시트를 읽어 게임에 넣을 번들을 만드는 CLI

사용법
  translation init                     설정 파일 견본을 만든다
  translation sync                     게임 DB 의 원문으로 번역 시트를 최신화한다
                                       (새 키 추가 · 원문 바뀐 키 표시 · 같은 원문 번역 추천 · 사라진 키 표시)
  translation validate                 번역 시트를 검사한다 (미번역, 플레이스홀더 불일치, 키 중복, 끝나지 않은 번역)
  translation export                   번역 시트를 읽어 언어별 번들 + 매니페스트를 만든다
  translation diff <이전> <이후>       매니페스트 두 개를 비교해 내려받을 언어를 알려준다

옵션
  -c, --config <경로>           설정 파일 (기본 translation.config.json)
  -o, --out <경로>              번들 출력 폴더 (설정의 output.directory를 덮어쓴다)
  -v, --version <문자열>        번들 버전 (비우면 UTC 타임스탬프)
      --strict                  검증 오류가 있으면 실패로 끝낸다 (validate는 미번역도 오류로 본다)
      --verbose                 경고와 이슈를 전부 출력한다
      --force                   init에서 기존 파일을 덮어쓴다
      --prune                   sync에서 게임 DB 에 없는 키의 행을 지운다 (기본은 표시만)
      --dry-run                 sync 결과만 보여 주고 시트에 쓰지 않는다

종료 코드
  0 성공   1 사용법 오류   2 실패   3 검증 오류

sync 만 게임 DB 를 읽고 시트에 쓴다. export 와 validate 는 시트를 읽기만 한다.";

        public const string StarterConfig = @"{
  ""//"": ""이 폴더가 번역 작업 폴더다. 시트 CSV 는 이 폴더에, 결과물은 Bundles/ 에 생긴다. 다른 자리를 쓰려면 translations · output 을 적는다."",

  ""sourceLanguage"": ""Korean"",
  ""languages"": [
    { ""id"": ""Korean"",  ""displayName"": ""한국어"",  ""bcp47"": ""ko"" },
    { ""id"": ""English"", ""displayName"": ""English"", ""bcp47"": ""en"", ""fallbackId"": ""Korean"" }
  ],

  ""//gameData"": ""sync 가 원문을 읽을 게임 DB 폴더. 테이블 이름 = 파일 이름. type 은 json · csv · excel."",
  ""gameData"": { ""type"": ""json"", ""path"": ""../Assets/GameData"" },

  ""//output"": ""export 결과. 비우면 ./Bundles. Unity 가 읽을 자리(Resources · StreamingAssets · 어드레서블 폴더)를 적어도 된다."",
  ""output"": { ""directory"": ""./Bundles"" },

  ""//targets"": ""번역할 것. 첫 줄은 UI 문자열(LocalizationData 표의 Key 컬럼), 나머지는 표와 컬럼. 표 번역은 전부 TranslationData.csv 한 장에 들어간다."",
  ""targets"": [
    { ""table"": """", ""sourceTable"": ""LocalizationData"", ""idColumn"": ""Key"" },
    { ""table"": ""ShipData"",  ""idColumn"": ""Id"", ""columns"": [""Name"", ""Description""] },
    { ""table"": ""SkillData"", ""idColumn"": ""Id"", ""columns"": [""Name""] }
  ]
}
";

    }
}
