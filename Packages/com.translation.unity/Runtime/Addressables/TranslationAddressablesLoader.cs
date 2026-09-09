using System;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary>
    /// 어드레서블에서 번들을 읽는다. 이 어셈블리는 asmdef defineConstraints 때문에
    /// com.unity.addressables 가 설치된 프로젝트에서만 컴파일된다
    /// </summary>
    public static class TranslationAddressablesLoader
    {
        /// <summary> 라벨 하나에 매니페스트와 언어 파일을 다 넣어 두고 읽는다 </summary>
        public static void FromAddressables(string label, Action<bool> done,
            string manifestName = null)
        {
            var handle = UnityEngine.AddressableAssets.Addressables
                .LoadAssetsAsync<TextAsset>(label, null);
            handle.Completed += op =>
            {
                if (op.Status != UnityEngine.ResourceManagement.AsyncOperations
                        .AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    Finish(done, Fail("어드레서블 라벨 로드 실패: " + label));
                    return;
                }

                var wanted = StripExtension(manifestName ?? BundleManifest.FileName);
                string manifestText = null;
                var byName = new System.Collections.Generic.Dictionary<string, string>(
                    op.Result.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var asset in op.Result)
                {
                    if (asset == null)
                        continue;

                    byName[asset.name] = asset.text;
                    if (string.Equals(asset.name, wanted, StringComparison.OrdinalIgnoreCase))
                        manifestText = asset.text;
                }

                if (manifestText == null)
                {
                    Finish(done, Fail("라벨 '" + label + "' 에 매니페스트가 없다: " + wanted));
                    return;
                }

                var manifest = BundleManifest.FromJson(manifestText);
                var languageId = TranslationRuntime.ResolveLanguage(manifest);
                var info = manifest.Find(languageId);
                if (info == null || !byName.TryGetValue(StripExtension(info.File), out var bundleText))
                {
                    Finish(done, Fail("라벨에 언어 번들이 없다: " + languageId));
                    return;
                }

                TranslationRuntime.Apply(new TranslationCatalog(
                    LanguageBundle.FromJson(bundleText), manifest));
                TranslationRuntime.RegisterReload(
                    next => FromAddressables(label, next, manifestName));
                Finish(done, true);
            };
        }

        private static string StripExtension(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var dot = name.IndexOf('.');
            return dot <= 0 ? name : name.Substring(0, dot);
        }

        private static bool Fail(string message)
        {
            Debug.LogError("[Translation] " + message);
            return false;
        }

        private static void Finish(Action<bool> done, bool ok)
        {
            if (done != null)
                done(ok);
        }
    }
}
