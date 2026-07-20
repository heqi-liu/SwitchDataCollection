using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SwitchDataCollection.Config;
using SwitchDataCollection.LoggerHelper;

namespace SwitchDataCollection.Function
{
    public class CsvParser
    {
        private const char _delimiter = ',';

        public List<Dictionary<string, object>> ParseFile(string filePath)
        {
            var config = ConfigManager.GetConfig().DataConfig;
            return ParseFile(filePath, config.ReadRows, config.ColumnIndices);
        }

        public List<Dictionary<string, object>> ParseFile(string filePath, int readRows)
        {
            var config = ConfigManager.GetConfig().DataConfig;
            return ParseFile(filePath, readRows, config.ColumnIndices);
        }

        public List<Dictionary<string, object>> ParseFile(string filePath, int readRows, int[] columnIndices)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            Logger.Info($"解析CSV: {filePath}, 行数: {readRows}");

            try
            {
                return ParseCsvFile(filePath, readRows, columnIndices);
            }
            catch (Exception ex)
            {
                Logger.Error($"解析失败: {filePath}", ex);
                throw;
            }
        }

        public List<Dictionary<string, object>> ParseLastNRows(string filePath, int n)
        {
            var config = ConfigManager.GetConfig().DataConfig;
            return ParseLastNRows(filePath, n, config.ColumnIndices);
        }

        public List<Dictionary<string, object>> ParseLastNRows(string filePath, int n, int[] columnIndices)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            Logger.Info($"读取最后 {n} 行: {filePath}");

            try
            {
                return ParseLastNRowsInternal(filePath, n, columnIndices);
            }
            catch (Exception ex)
            {
                Logger.Error($"读取失败: {filePath}", ex);
                throw;
            }
        }

        private List<Dictionary<string, object>> ParseCsvFile(string filePath, int readRows, int[] columnIndices)
        {
            var result = new List<Dictionary<string, object>>();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var encoding = DetectEncoding(stream);
                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, encoding))
                {
                    string[] headers = null;
                    int rowIndex = 0;
                    int dataRowCount = 0;

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            rowIndex++;
                            continue;
                        }

                        var values = ParseCsvLine(line);

                        if (rowIndex == 0)
                        {
                            headers = values;
                            rowIndex++;
                            continue;
                        }

                        if (dataRowCount >= readRows) break;

                        var rowData = BuildRowData(values, headers, columnIndices);
                        if (rowData.Any())
                        {
                            result.Add(rowData);
                            dataRowCount++;
                        }

                        rowIndex++;
                    }
                }
            }

            return result;
        }

        private List<Dictionary<string, object>> ParseLastNRowsInternal(string filePath, int n, int[] columnIndices)
        {
            var result = new List<Dictionary<string, object>>();

            var lastLines = ReadLastNLines(filePath, n + 1);
            if (lastLines.Count == 0) return result;

            string[] headers = null;
            string headerLine = ReadFirstLine(filePath);
            if (headerLine != null) headers = ParseCsvLine(headerLine);

            var dataLines = lastLines;
            if (dataLines.Count > n) dataLines = dataLines.Skip(dataLines.Count - n).ToList();

            foreach (var line in dataLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var values = ParseCsvLine(line);
                var rowData = BuildRowData(values, headers, columnIndices);
                if (rowData.Any()) result.Add(rowData);
            }

            return result;
        }

        private List<string> ReadLastNLines(string filePath, int n)
        {
            var lines = new List<string>();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var encoding = DetectEncoding(stream);
                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, encoding))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lines.Add(line);
                            if (lines.Count > n + 1)
                            {
                                lines.RemoveAt(0);
                            }
                        }
                    }
                }
            }

            if (lines.Count > 0 && lines.Count <= n + 1)
            {
                lines = lines.Skip(Math.Max(0, lines.Count - n)).ToList();
            }

            return lines;
        }

        private string ReadFirstLine(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var encoding = DetectEncoding(stream);
                stream.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(stream, encoding, false, 1024);
                try
                {
                    return reader.ReadLine();
                }
                finally
                {
                    reader.Dispose();
                }
            }
        }

        private int GetBomLength(Encoding encoding)
        {
            if (encoding is UTF8Encoding) return 3;
            if (encoding is UnicodeEncoding) return 2;
            if (encoding.BodyName == "utf-16BE") return 2;
            if (encoding is UTF32Encoding) return 4;
            return 0;
        }

        private Dictionary<string, object> BuildRowData(string[] values, string[] headers, int[] columnIndices)
        {
            var rowData = new Dictionary<string, object>();

            if (columnIndices == null || columnIndices.Length == 0)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    string header = headers != null && headers.Length > i ? headers[i] : $"Column_{i}";
                    rowData[header] = ParseValue(values[i]);
                }
            }
            else
            {
                foreach (int colIndex in columnIndices)
                {
                    if (colIndex >= values.Length) continue;
                    string header = headers != null && headers.Length > colIndex ? headers[colIndex] : $"Column_{colIndex}";
                    rowData[header] = ParseValue(values[colIndex]);
                }
            }

            return rowData;
        }

        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var currentValue = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentValue.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == _delimiter && !inQuotes)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                    continue;
                }

                currentValue.Append(c);
            }

            values.Add(currentValue.ToString().Trim());
            return values.ToArray();
        }

        private object ParseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            
            string converted = ConvertChineseDate(value);
            return converted;
        }

        private string ConvertChineseDate(string value)
        {
            if (value.Contains("月") && value.Contains("日"))
            {
                int monthStart = 0;
                int monthEnd = value.IndexOf("月");
                int dayStart = monthEnd + 1;
                int dayEnd = value.IndexOf("日");

                if (monthEnd > 0 && dayEnd > monthEnd)
                {
                    string monthStr = value.Substring(monthStart, monthEnd);
                    string dayStr = value.Substring(dayStart, dayEnd - dayStart);

                    if (int.TryParse(monthStr, out int month) && int.TryParse(dayStr, out int day))
                    {
                        return $"{month:00}/{day:00}";
                    }
                }
            }
            return value;
        }

        private Encoding DetectEncoding(Stream stream)
        {
            byte[] bom = new byte[4];
            int bytesRead = stream.Read(bom, 0, 4);

            if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (bytesRead >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                return Encoding.UTF32;

            return Encoding.Default;
        }
    }
}
