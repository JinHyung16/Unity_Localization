using System;
using System.IO;
using UnityEngine;

namespace Translation.Unity
{
    /// <summary> 내려받은 번들을 기기에 보관한다. 다운로드 자체는 프로젝트가 한다 </summary>
    public sealed class TranslationBundleCache
    {
        public const string DefaultFolderName = "translation";

        public string Directory { get; }

        public TranslationBundleCache(string directory = null)
        {
            Directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.persistentDataPath, DefaultFolderName)
                : directory;
        }

        /// <summary> 보관된 매니페스트. 없거나 손상됐으면 null </summary>
        public BundleManifest LoadManifest()
        {
            var path = Path.Combine(Directory, BundleManifest.FileName);
            if (!File.Exists(path))
                return null;

            try
            {
                return BundleReader.ReadManifest(File.ReadAllBytes(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Translation] 보관된 매니페스트를 읽을 수 없어 무시합니다: " + e.Message);
                return null;
            }
        }

        public ManifestDiff Compare(BundleManifest remote)
        {
            return ManifestDiff.Compare(LoadManifest(), remote);
        }

        public bool Has(string fileName)
        {
            return File.Exists(PathFor(fileName));
        }

        public byte[] Load(string fileName)
        {
            var path = PathFor(fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary> 언어 파일을 보관한다. 매니페스트의 해시와 맞지 않으면 거부한다 </summary>
        public void Save(string fileName, byte[] bytes, string expectedHash = null)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("fileName은 필수입니다.", nameof(fileName));

            if (bytes == null || bytes.Length == 0)
                throw new TranslationBundleException("보관할 바이트가 비어 있습니다: " + fileName);

            if (!string.IsNullOrEmpty(expectedHash) && !BundleReader.VerifyHash(bytes, expectedHash))
                throw new TranslationBundleException(
                    "내려받은 번들의 해시가 매니페스트와 다릅니다: " + fileName + " (전송 중 손상)");

            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllBytes(PathFor(fileName), bytes);
        }

        /// <summary> 언어 파일을 다 보관한 뒤 마지막에 부른다. 중간에 끊겨도 다음 실행이 다시 받는다 </summary>
        public void SaveManifest(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new TranslationBundleException("매니페스트 바이트가 비어 있습니다.");

            BundleReader.ReadManifest(bytes);

            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllBytes(Path.Combine(Directory, BundleManifest.FileName), bytes);
        }

        /// <summary> 매니페스트에 적힌 언어 파일이 모두 있고 해시도 맞는지 </summary>
        public bool IsComplete(BundleManifest manifest)
        {
            if (manifest == null)
                return false;

            foreach (var language in manifest.Languages)
            {
                var bytes = Load(language.File);
                if (bytes == null)
                    return false;

                if (!string.IsNullOrEmpty(language.Hash) && !BundleReader.VerifyHash(bytes, language.Hash))
                    return false;
            }

            return true;
        }

        public void Clear()
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, true);
        }

        private string PathFor(string fileName)
        {
            return Path.Combine(Directory, fileName);
        }
    }
}
