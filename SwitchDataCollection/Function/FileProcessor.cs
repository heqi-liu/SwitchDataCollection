using SwitchDataCollection.LoggerHelper;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SwitchDataCollection.Function
{
    public static class FileProcessor
    {
        public static string FindLatestCsvFile(string folderPath, string includePattern = null, string excludePattern = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentNullException(nameof(folderPath));

            if (!Directory.Exists(folderPath))
            {
                Logger.Warning($"目录不存在: {folderPath}");
                return null;
            }

            try
            {
                string[] txtFiles = Directory.GetFiles(folderPath, "*.txt", SearchOption.AllDirectories);
                string[] csvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.AllDirectories);
                string[] allFiles = new string[txtFiles.Length + csvFiles.Length];
                Array.Copy(txtFiles, allFiles, txtFiles.Length);
                Array.Copy(csvFiles, 0, allFiles, txtFiles.Length, csvFiles.Length);

                string[] filteredFiles = FilterFiles(allFiles, includePattern, excludePattern);

                if (filteredFiles.Length == 0)
                {
                    Logger.Warning($"未找到符合条件的文件: {folderPath}");
                    return null;
                }

                string latestFile = null;
                DateTime latestTime = DateTime.MinValue;

                foreach (string filePath in filteredFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTime > latestTime)
                    {
                        latestTime = fileInfo.LastWriteTime;
                        latestFile = filePath;
                    }
                }

                return latestFile;
            }
            catch (Exception ex)
            {
                Logger.Error($"查找文件失败: {folderPath}", ex);
                return null;
            }
        }

        private static string[] FilterFiles(string[] files, string includePattern, string excludePattern)
        {
            if (files == null || files.Length == 0)
                return new string[0];

            var filtered = new System.Collections.Generic.List<string>();

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                bool includeMatch = true;
                if (!string.IsNullOrWhiteSpace(includePattern))
                {
                    includeMatch = Regex.IsMatch(fileName, includePattern);
                }

                bool excludeMatch = false;
                if (!string.IsNullOrWhiteSpace(excludePattern))
                {
                    excludeMatch = Regex.IsMatch(fileName, excludePattern);
                }

                if (includeMatch && !excludeMatch)
                {
                    filtered.Add(file);
                }
            }

            return filtered.ToArray();
        }
    }
}