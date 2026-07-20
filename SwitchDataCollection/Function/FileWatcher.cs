using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SwitchDataCollection.LoggerHelper;

namespace SwitchDataCollection.Function
{
    public class FileWatcher
    {
        private FileSystemWatcher _watcher;
        private string _folderPath;
        private Timer _debounceTimer;
        private const int _debounceDelay = 500;
        private ConcurrentDictionary<string, byte> _pendingFiles = new ConcurrentDictionary<string, byte>();
        private string _includePattern;
        private string _excludePattern;

        public event EventHandler<FileChangedEventArgs> FileChanged;

        public FileWatcher(string folderPath, string includePattern = null, string excludePattern = null)
        {
            _folderPath = folderPath;
            _includePattern = includePattern;
            _excludePattern = excludePattern;
        }

        public void Start()
        {
            if (!Directory.Exists(_folderPath))
            {
                Logger.Warning($"目录不存在，创建: {_folderPath}");
                Directory.CreateDirectory(_folderPath);
            }

            _watcher = new FileSystemWatcher(_folderPath, "*.*")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536,
                IncludeSubdirectories = true
            };

            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

            Logger.Info($"文件监听已启动: {_folderPath}");
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            Logger.Info($"文件监听已停止: {_folderPath}");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Logger.Info($"文件系统事件: {e.ChangeType} - {e.FullPath}");
            if (e.ChangeType == WatcherChangeTypes.Deleted) 
            {
                Logger.Info("文件已删除，跳过");
                return;
            }
            if (!File.Exists(e.FullPath)) 
            {
                Logger.Info("文件不存在，跳过");
                return;
            }

            string fileName = Path.GetFileName(e.FullPath);
            if (!MatchPattern(fileName))
            {
                Logger.Info($"文件不符合过滤条件，跳过: {fileName}");
                return;
            }

            _pendingFiles[e.FullPath] = 0;
            Logger.Info($"添加到待处理队列: {e.FullPath}");
            ResetDebounceTimer();
        }

        private bool MatchPattern(string fileName)
        {
            bool includeMatch = true;
            if (!string.IsNullOrWhiteSpace(_includePattern))
            {
                if (_includePattern.StartsWith("$"))
                {
                    string prefix = _includePattern.Substring(1);
                    includeMatch = fileName.StartsWith(prefix);
                }
                else
                {
                    includeMatch = fileName.Contains(_includePattern);
                }
            }

            bool excludeMatch = false;
            if (!string.IsNullOrWhiteSpace(_excludePattern))
            {
                if (_excludePattern.StartsWith("$"))
                {
                    string prefix = _excludePattern.Substring(1);
                    excludeMatch = fileName.StartsWith(prefix);
                }
                else
                {
                    excludeMatch = fileName.Contains(_excludePattern);
                }
            }

            return includeMatch && !excludeMatch;
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (File.Exists(e.FullPath))
            {
                _pendingFiles[e.FullPath] = 0;
            }
            ResetDebounceTimer();
        }

        private void ResetDebounceTimer()
        {
            _debounceTimer.Change(_debounceDelay, Timeout.Infinite);
        }

        private void OnDebounceElapsed(object state)
        {
            try
            {
                string[] files;
                lock (_pendingFiles)
                {
                    files = _pendingFiles.Keys.ToArray();
                    _pendingFiles.Clear();
                }

                if (files.Length == 0) return;

                foreach (var file in files)
                {
                    if (!File.Exists(file)) continue;
                    Logger.Info($"检测到文件变化: {file}");
                    FileChanged?.Invoke(this, new FileChangedEventArgs(file));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("文件变化处理异常", ex);
            }
        }
    }
}
