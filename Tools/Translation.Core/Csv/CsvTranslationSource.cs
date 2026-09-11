using System;
using System.Collections.Generic;
using System.IO;

namespace Translation
{
    /// <summary> 폴더 안의 {시트명}.csv 를 번역 시트로 읽는다. sync 는 같은 파일에 쓴다 </summary>
    public sealed class CsvTranslationSource : ITranslationSource, ITranslationSheetStore
    {
        private readonly string _directory;

        public string FileExtension { get; set; } = ".csv";

        public CsvTranslationSource(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory는 필수입니다.", nameof(directory));

            _directory = directory;
        }

        public string Label
        {
            get { return "csv:" + _directory; }
        }

        public bool SheetExists(string sheetName)
        {
            return File.Exists(PathFor(sheetName));
        }

        public IReadOnlyList<TranslateEntry> Read(SheetLayout layout, IList<string> warnings)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var path = PathFor(layout.SheetName);
            if (!File.Exists(path))
                return new List<TranslateEntry>();

            var rows = CsvFile.Read(path);
            var wrapped = new List<IReadOnlyList<string>>(rows.Count);
            foreach (var row in rows)
                wrapped.Add(row);

            return layout.ReadEntries(wrapped, warnings);
        }

        public List<string[]> ReadGrid(string sheetName)
        {
            var path = PathFor(sheetName);
            return File.Exists(path) ? CsvFile.Read(path) : new List<string[]>();
        }

        public void WriteGrid(string sheetName, IReadOnlyList<string[]> rows)
        {
            CsvFile.Write(PathFor(sheetName), rows);
        }

        private string PathFor(string sheetName)
        {
            return Path.Combine(_directory, sheetName + FileExtension);
        }
    }
}
