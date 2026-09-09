using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Translation.Unity
{
    /// <summary>
    /// 번들을 읽는 표준 경로. 어드레서블은 Translation.Unity.Addressables 어셈블리에 있다.
    /// 바이트를 이미 갖고 있으면 <see cref="TranslationRuntime.Load"/> 를 직접 불러도 된다
    /// </summary>
    public static class TranslationLoader
    {
        /// <summary> Resources 에서 읽는다. 파일이 .json 으로 구워져 TextAsset 으로 임포트돼 있어야 한다 </summary>
        public static bool FromResources(string directory, string manifestName = null)
        {
            var manifestText = LoadText(directory, StripExtension(
                manifestName ?? BundleManifest.FileName));
            if (manifestText == null)
                return Fail("Resources 에 매니페스트가 없다: " + Join(directory, manifestName));

            var manifest = BundleManifest.FromJson(manifestText);
            var languageId = TranslationRuntime.ResolveLanguage(manifest);
            var info = manifest.Find(languageId);
            if (info == null)
                return Fail("매니페스트에 언어가 없다: " + languageId);

            var bundleText = LoadText(directory, StripExtension(info.File));
            if (bundleText == null)
                return Fail("Resources 에 언어 번들이 없다: " + Join(directory, info.File));

            TranslationRuntime.Apply(new TranslationCatalog(
                LanguageBundle.FromJson(bundleText), manifest));
            TranslationRuntime.RegisterReload(done => done(FromResources(directory, manifestName)));
            return true;
        }

        /// <summary>
        /// 전 언어를 담은 CSV 한 장을 읽는다. 매니페스트가 필요 없고,
        /// 언어는 저장된 선택 → <see cref="TranslateOption"/> → 기기 언어 이름 순으로 정한다
        /// </summary>
        public static bool FromResourcesCsv(string path)
        {
            var asset = Resources.Load<TextAsset>(StripExtension(path));
            if (asset == null)
                return Fail("Resources 에 CSV 가 없다: " + path);

            var languageId = TranslationRuntime.LoadSavedLanguage()
                ?? TranslateOption.ResolveDisplayLanguage()
                ?? Application.systemLanguage.ToString();

            var values = LocalKeyCsv.Parse(asset.text, languageId);
            if (values.Count == 0)
                return Fail("CSV 에 언어 열이 없거나 비어 있다: " + languageId);

            var bundle = new LanguageBundle(languageId, values.Count);
            foreach (var pair in values)
                bundle.Set(pair.Key, pair.Value);

            TranslationRuntime.Apply(new TranslationCatalog(bundle));
            TranslationRuntime.RegisterReload(done => done(FromResourcesCsv(path)));
            return true;
        }

        /// <summary> StreamingAssets 에서 읽는다. UnityWebRequest 라 결과가 콜백으로 온다 </summary>
        public static void FromStreamingAssets(string directory, Action<bool> done,
            string manifestName = null)
        {
            var root = Path(Application.streamingAssetsPath, directory);
            Get(Path(root, manifestName ?? BundleManifest.FileName), manifestText =>
            {
                if (manifestText == null)
                {
                    Finish(done, Fail("StreamingAssets 에 매니페스트가 없다: " + root));
                    return;
                }

                var manifest = BundleManifest.FromJson(manifestText);
                var languageId = TranslationRuntime.ResolveLanguage(manifest);
                var info = manifest.Find(languageId);
                if (info == null)
                {
                    Finish(done, Fail("매니페스트에 언어가 없다: " + languageId));
                    return;
                }

                Get(Path(root, info.File), bundleText =>
                {
                    if (bundleText == null)
                    {
                        Finish(done, Fail("StreamingAssets 에 언어 번들이 없다: " + info.File));
                        return;
                    }

                    TranslationRuntime.Apply(new TranslationCatalog(
                        LanguageBundle.FromJson(bundleText), manifest));
                    TranslationRuntime.RegisterReload(
                        next => FromStreamingAssets(directory, next, manifestName));
                    Finish(done, true);
                });
            });
        }


        private static string LoadText(string directory, string name)
        {
            var asset = Resources.Load<TextAsset>(Join(directory, name));
            return asset == null ? null : asset.text;
        }

        private static void Get(string url, Action<string> done)
        {
            var request = UnityWebRequest.Get(FileUrl(url));
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                var ok = request.result == UnityWebRequest.Result.Success;
                var text = ok ? request.downloadHandler.text : null;
                request.Dispose();
                done(text);
            };
        }

        private static string FileUrl(string path)
        {
            return path.Contains("://") ? path : "file://" + path;
        }

        private static string Path(string a, string b)
        {
            if (string.IsNullOrEmpty(b))
                return a;

            return a.TrimEnd('/', '\\') + "/" + b.TrimStart('/', '\\');
        }

        private static string Join(string directory, string name)
        {
            return string.IsNullOrEmpty(directory) ? name : Path(directory, name);
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
