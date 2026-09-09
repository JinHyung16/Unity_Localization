using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;

namespace Translation.Excel
{
    /// <summary>
    /// 엑셀 경로를 시트 단위로 해석한다. 폴더면 {이름}.xlsx 파일 하나,
    /// .xlsx 파일이면 그 안의 워크시트 이름에 대응한다
    /// </summary>
    public sealed class ExcelWorkbookSet : IDisposable
    {
        private const int MaxWorksheetName = 31;

        private readonly Dictionary<string, XLWorkbook> _open =
            new Dictionary<string, XLWorkbook>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _resolved =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _dirty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly bool _singleWorkbook;

        public string Path { get; }

        public string Extension { get; set; } = ".xlsx";

        public ExcelWorkbookSet(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path는 필수입니다.", nameof(path));

            Path = path;
            _singleWorkbook = File.Exists(path)
                              && string.Equals(
                                  System.IO.Path.GetExtension(path),
                                  ".xlsx",
                                  StringComparison.OrdinalIgnoreCase);

            if (!_singleWorkbook && !Directory.Exists(path))
                throw new TranslationConfigException("엑셀 경로가 폴더도 .xlsx 파일도 아닙니다: " + path);
        }

        public bool IsSingleWorkbook
        {
            get { return _singleWorkbook; }
        }

        public string Label
        {
            get { return "excel:" + Path; }
        }

        public bool SheetExists(string name)
        {
            if (_singleWorkbook)
                return GetWorkbook(Path).Worksheets.TryGetWorksheet(name, out _);

            return File.Exists(FilePathFor(name));
        }

        /// <summary> 워크시트를 읽기용으로 가져온다. 없으면 null </summary>
        public IXLWorksheet TryGetWorksheet(string name)
        {
            if (_singleWorkbook)
                return GetWorkbook(Path).Worksheets.TryGetWorksheet(name, out var sheet) ? sheet : null;

            var file = FilePathFor(name);
            if (!File.Exists(file))
                return null;

            var workbook = GetWorkbook(file);
            if (workbook.Worksheets.TryGetWorksheet(name, out var named))
                return named;

            return workbook.Worksheets.Count > 0 ? workbook.Worksheet(1) : null;
        }

        /// <summary> 워크시트를 쓰기용으로 가져온다. 없으면 만든다 (폴더 모드면 파일도 만든다) </summary>
        public IXLWorksheet GetOrCreateWorksheet(string name)
        {
            var file = _singleWorkbook ? Path : FilePathFor(name);
            _dirty.Add(file);

            var existing = TryGetWorksheet(name);
            if (existing != null)
                return existing;

            XLWorkbook workbook;
            if (File.Exists(file))
            {
                workbook = GetWorkbook(file);
            }
            else
            {
                workbook = new XLWorkbook();
                _open[file] = workbook;
            }

            var title = name.Length > MaxWorksheetName ? name.Substring(0, MaxWorksheetName) : name;
            return workbook.Worksheets.Add(title);
        }

        /// <summary> GetOrCreateWorksheet 로 만진 통합문서를 저장한다 </summary>
        public void Save()
        {
            foreach (var file in _dirty)
            {
                if (!_open.TryGetValue(file, out var workbook))
                    continue;

                var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file));
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                workbook.SaveAs(file);
            }

            _dirty.Clear();
        }

        public void Dispose()
        {
            foreach (var pair in _open)
                pair.Value.Dispose();

            _open.Clear();
        }

        // 폴더 모드: 루트에 없으면 하위 폴더에서 찾는다.
        // LocalKey 시트와 TranslationKey 시트를 다른 폴더에 둘 수 있게 하려고 재귀로 본다
        private string FilePathFor(string name)
        {
            var direct = System.IO.Path.Combine(Path, name + Extension);
            if (File.Exists(direct))
                return direct;

            if (_resolved.TryGetValue(name, out var cached))
                return cached;

            var found = direct;
            try
            {
                var hits = Directory.GetFiles(Path, name + Extension, SearchOption.AllDirectories);
                if (hits.Length > 0)
                    found = hits[0];
            }
            catch (DirectoryNotFoundException)
            {
            }

            _resolved[name] = found;
            return found;
        }

        private XLWorkbook GetWorkbook(string file)
        {
            if (_open.TryGetValue(file, out var cached))
                return cached;

            if (!File.Exists(file))
                throw new TranslationConfigException("엑셀 파일이 없습니다: " + file);

            var workbook = new XLWorkbook(file);
            _open[file] = workbook;
            return workbook;
        }
    }
}
