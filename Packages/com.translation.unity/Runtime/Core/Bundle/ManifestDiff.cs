using System;
using System.Collections.Generic;

namespace Translation
{
    public sealed class ManifestDiff
    {
        public List<string> AddedLanguages { get; } = new List<string>();

        /// <summary> 해시가 달라진 언어. 이 파일만 다시 받으면 된다 </summary>
        public List<string> ChangedLanguages { get; } = new List<string>();

        public List<string> RemovedLanguages { get; } = new List<string>();

        public List<string> UnchangedLanguages { get; } = new List<string>();

        public bool HasChanges
        {
            get
            {
                return AddedLanguages.Count > 0
                       || ChangedLanguages.Count > 0
                       || RemovedLanguages.Count > 0;
            }
        }

        public IReadOnlyList<string> LanguagesToFetch
        {
            get
            {
                var list = new List<string>(AddedLanguages.Count + ChangedLanguages.Count);
                list.AddRange(AddedLanguages);
                list.AddRange(ChangedLanguages);
                return list;
            }
        }

        /// <summary> local이 null이면 전부 새로 받아야 하는 것으로 본다 </summary>
        public static ManifestDiff Compare(BundleManifest local, BundleManifest remote)
        {
            if (remote == null)
                throw new ArgumentNullException(nameof(remote));

            var diff = new ManifestDiff();

            foreach (var remoteLang in remote.Languages)
            {
                var localLang = local?.Find(remoteLang.Id);
                if (localLang == null)
                {
                    diff.AddedLanguages.Add(remoteLang.Id);
                    continue;
                }

                if (string.Equals(localLang.Hash, remoteLang.Hash, StringComparison.OrdinalIgnoreCase))
                    diff.UnchangedLanguages.Add(remoteLang.Id);
                else
                    diff.ChangedLanguages.Add(remoteLang.Id);
            }

            if (local != null)
            {
                foreach (var localLang in local.Languages)
                {
                    if (remote.Find(localLang.Id) == null)
                        diff.RemovedLanguages.Add(localLang.Id);
                }
            }

            return diff;
        }

        public override string ToString()
        {
            return "added=" + AddedLanguages.Count
                   + ", changed=" + ChangedLanguages.Count
                   + ", removed=" + RemovedLanguages.Count
                   + ", unchanged=" + UnchangedLanguages.Count;
        }
    }
}
