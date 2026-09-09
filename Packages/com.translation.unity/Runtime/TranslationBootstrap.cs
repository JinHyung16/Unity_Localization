using System;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary>
    /// 게임 시작 때 부르는 한 줄. 빌드에 넣어 둔 LocalKey.csv 로 먼저 글자를 띄우고,
    /// 번들이 오면 그걸로 바꿔 끼운다. 번들이 못 와도 CSV 글자로 계속 간다.
    /// 화면의 LocalizeText 는 바뀔 때마다 알아서 다시 그려진다
    /// </summary>
    public static class TranslationBootstrap
    {
        /// <summary>
        /// csvResourcesPath: Resources 안의 LocalKey.csv 자리 (예: "Localization/LocalKey"). 비우면 건너뛴다.
        /// loadBundle: 번들을 읽는 법. 끝나면 받은 콜백을 불러 준다 (예: next => TranslationLoader.FromStreamingAssets("Localization", next)).
        /// done: CSV 든 번들이든 하나라도 읽혔으면 true
        /// </summary>
        public static void Start(string csvResourcesPath, Action<Action<bool>> loadBundle, Action<bool> done = null)
        {
            var quick = !string.IsNullOrEmpty(csvResourcesPath) && TranslationLoader.FromResourcesCsv(csvResourcesPath);

            if (loadBundle == null)
            {
                done?.Invoke(quick);
                return;
            }

            try
            {
                loadBundle(ok =>
                {
                    if (!ok)
                        Debug.LogWarning("[Translation] 번들을 못 읽어 " + (quick ? "LocalKey.csv 글자로 계속 갑니다." : "글자가 없습니다."));

                    done?.Invoke(ok || quick);
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[Translation] 번들 읽기 중 오류: " + e.Message);
                done?.Invoke(quick);
            }
        }

        /// <summary> CSV 와 번들이 둘 다 Resources 에 있을 때. 동기라 바로 결과가 온다 </summary>
        public static bool StartFromResources(string csvResourcesPath, string bundleDirectory)
        {
            var result = false;
            Start(csvResourcesPath, next => next(TranslationLoader.FromResources(bundleDirectory)), ok => result = ok);
            return result;
        }

        /// <summary> CSV 는 Resources, 번들은 StreamingAssets 에 있을 때 </summary>
        public static void StartFromStreamingAssets(string csvResourcesPath, string bundleDirectory, Action<bool> done = null)
        {
            Start(csvResourcesPath, next => TranslationLoader.FromStreamingAssets(bundleDirectory, next), done);
        }
    }
}
