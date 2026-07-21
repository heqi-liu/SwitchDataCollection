using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SwitchDataCollection.Config;
using SwitchDataCollection.Function;
using SwitchDataCollection.LoggerHelper;
using SwitchDataCollection.Model;
using SwitchDataCollection.PLCHelper;

namespace SwitchDataCollection
{
    class Program
    {
        private static CsvParser _csvParser;
        private static IPlcCommunicator _plcCommunicator;
        private static FileWatcher _fileWatcher;
        private static FileSystemWatcher _configWatcher;
        private static Timer _configDebounceTimer;
        private static Timer _sendTimer;
        private static Timer _logCleanupTimer;
        private static ConcurrentQueue<DataRecord> _dataQueue = new ConcurrentQueue<DataRecord>();
        private static bool _isPlcConnected;
        private static ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static ManualResetEvent _timerCallbackDone = new ManualResetEvent(true);
        private static volatile bool _isExiting;
        private static object _processLock = new object();

        static void Main(string[] args)
        {
            try
            {
                InitializeApplication();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; _exitEvent.Set(); };
                _exitEvent.WaitOne();
                Cleanup();
            }
            catch (Exception ex)
            {
                Logger.Error("程序运行异常", ex);
            }
        }

        private static void InitializeApplication()
        {
            var config = ConfigManager.GetConfig();
            Logger.Info($"配置加载成功 - 目标目录: {config.DataConfig.TargetFolderPath}, 读取行数: {config.DataConfig.ReadRows}");

            _csvParser = new CsvParser();

            if (config.PlcCommunication.Enabled)
            {
                _plcCommunicator = new PlcCommunicator();
                Logger.Info($"PLC通信器初始化完成: {config.PlcCommunication.IpAddress}:{config.PlcCommunication.Port}");
                StartSendTimer();
            }

            _fileWatcher = new FileWatcher(config.DataConfig.TargetFolderPath, config.DataConfig.TargetFileInclude, config.DataConfig.TargetFileNoInclude);
            _fileWatcher.FileChanged += (s, e) => OnFileChanged(s, e.FilePath);
            _fileWatcher.Start();

            StartConfigWatcher();

            CheckLatestFileOnStartup();

            CleanupOldLogsOnStartup();
            StartLogCleanupTimer();

            Logger.Info("程序启动，等待文件变化...");
        }

        private static void CheckLatestFileOnStartup()
        {
            try
            {
                var config = ConfigManager.GetConfig().DataConfig;
                string latestFile = FileProcessor.FindLatestCsvFile(config.TargetFolderPath, config.TargetFileInclude, config.TargetFileNoInclude);
                if (!string.IsNullOrEmpty(latestFile))
                {
                    OnFileChanged(null, latestFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("启动时检查文件失败", ex);
            }
        }

        private static void CleanupOldLogsOnStartup()
        {
            try
            {
                int retentionDays = ConfigManager.GetConfig().DataConfig.LogRetentionDays;
                Logger.CleanupOldLogs(retentionDays);
            }
            catch (Exception ex)
            {
                Logger.Error("启动时清理过期日志失败", ex);
            }
        }

        private static void StartLogCleanupTimer()
        {
            int interval = 24 * 60 * 60 * 1000;
            _logCleanupTimer = new Timer(CleanupOldLogsTimerCallback, null, interval, interval);
        }

        private static void CleanupOldLogsTimerCallback(object state)
        {
            try
            {
                int retentionDays = ConfigManager.GetConfig().DataConfig.LogRetentionDays;
                Logger.CleanupOldLogs(retentionDays);
            }
            catch (Exception ex)
            {
                Logger.Error("定时清理过期日志失败", ex);
            }
        }

        private static void StartSendTimer()
        {
            int interval = ConfigManager.GetConfig().PlcCommunication.WriteIntervalSeconds * 1000;
            _sendTimer = new Timer(SendDataToPlc, null, interval, interval);
        }

        private static void StartConfigWatcher()
        {
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir))
            {
                Logger.Warning($"配置目录不存在: {configDir}");
                return;
            }

            _configWatcher = new FileSystemWatcher(configDir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Renamed += OnConfigFileChanged;

            _configDebounceTimer = new Timer(OnConfigDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            _configDebounceTimer.Change(500, Timeout.Infinite);
        }

        private static void OnConfigDebounceElapsed(object state)
        {
            try
            {
                ConfigManager.ReloadConfig();
                _plcCommunicator?.ReloadConfig();
                RestartSendTimer();

                var config = ConfigManager.GetConfig();
                _fileWatcher.Stop();
                _fileWatcher = new FileWatcher(config.DataConfig.TargetFolderPath, config.DataConfig.TargetFileInclude, config.DataConfig.TargetFileNoInclude);
                _fileWatcher.FileChanged += (s, e) => OnFileChanged(s, e.FilePath);
                _fileWatcher.Start();
                Logger.Info($"配置已更新: {config.DataConfig.TargetFolderPath}");
            }
            catch (IOException ex)
            {
                Logger.Warning($"配置文件被占用，稍后重试: {ex.Message}");
                _configDebounceTimer.Change(500, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error("重新加载配置失败", ex);
            }
        }

        private static void RestartSendTimer()
        {
            _sendTimer?.Dispose();
            StartSendTimer();
        }

        private static void OnFileChanged(object sender, string latestFile)
        {
            if (!Monitor.TryEnter(_processLock)) return;

            try
            {
                ProcessFile(latestFile);
            }
            finally
            {
                Monitor.Exit(_processLock);
            }
        }

        private static void ProcessFile(string filePath)
        {
            try
            {
                var config = ConfigManager.GetConfig().DataConfig;
                var parsedData = _csvParser.ParseLastNRows(filePath, config.ReadRows);
                if (parsedData == null || parsedData.Count == 0) return;

                var records = DataConverter.CreateRecords(parsedData);

                if (config.ReadTargetFileName)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    string[] segments = fileNameWithoutExtension.Split('@');
                    if (segments.Length >= 3)
                    {
                        string fileNamePrefix = segments[1] + "," + segments[2] + ",";
                        foreach (var record in records)
                        {
                            record.FileNamePrefix = fileNamePrefix;
                        }
                        Logger.Info($"文件名前缀提取成功: {fileNamePrefix}");
                    }
                    else
                    {
                        Logger.Warning($"文件名格式不符合预期，无法提取前缀: {fileNameWithoutExtension}");
                    }
                }

                LogData(records);
                EnqueueData(records);
            }
            catch (Exception ex)
            {
                Logger.Error("处理文件异常", ex);
            }
        }

        private static void SendDataToPlc(object state)
        {
            if (_isExiting) return;
            
            _timerCallbackDone.Reset();
            try
            {
                if (_dataQueue.Count == 0) return;

                var config = ConfigManager.GetConfig().PlcCommunication;
                if (!config.Enabled || _plcCommunicator == null) return;

                if (!_isPlcConnected)
                {
                    _isPlcConnected = _plcCommunicator.Connect();
                    if (!_isPlcConnected)
                    {
                        Logger.Error("PLC连接失败");
                        return;
                    }
                }

                var records = DequeueAllData();
                if (records.Count == 0) return;

                var batch = new DataBatch { Records = records, TotalRecords = records.Count };
                bool result = _plcCommunicator.SendBatchData(batch);
                
                if (result)
                {
                    Logger.Info($"发送成功: {records.Count} 条");
                }
                else
                {
                    _isPlcConnected = false;
                    _plcCommunicator.Disconnect();
                    
                    if (_plcCommunicator.Connect())
                    {
                        _isPlcConnected = true;
                        result = _plcCommunicator.SendBatchData(batch);
                        if (result)
                        {
                            Logger.Info($"重连后发送成功: {records.Count} 条");
                        }
                        else
                        {
                            Logger.Error("重连后发送仍失败");
                            EnqueueData(records);
                            _isPlcConnected = false;
                        }
                    }
                    else
                    {
                        Logger.Error("PLC重连失败");
                        EnqueueData(records);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("发送数据异常", ex);
                _isPlcConnected = false;
            }
            finally
            {
                _timerCallbackDone.Set();
            }
        }

        private static List<DataRecord> DequeueAllData()
        {
            var records = new List<DataRecord>();
            while (_dataQueue.TryDequeue(out DataRecord record))
            {
                records.Add(record);
            }
            return records;
        }

        private static void EnqueueData(List<DataRecord> records)
        {
            int maxSize = ConfigManager.GetConfig().DataConfig.QueueMaxSize;
            
            while (_dataQueue.Count + records.Count > maxSize)
            {
                _dataQueue.TryDequeue(out _);
            }
            
            foreach (var record in records)
            {
                _dataQueue.Enqueue(record);
            }
            Logger.Info($"已累积数据: {_dataQueue.Count} 条");
        }

        private static void LogData(List<DataRecord> records)
        {
            Logger.Info($"解析数据: {records.Count} 行");
            foreach (var record in records)
            {
                string prefix = !string.IsNullOrWhiteSpace(record.FileNamePrefix) ? $"[{record.FileNamePrefix}] " : "";
                Logger.Info(prefix + string.Join(",", record.Fields.Values));
            }
        }

        private static void Cleanup()
        {
            Logger.Info("程序退出，清理资源");
            
            _isExiting = true;
            
            _sendTimer?.Dispose();
            _logCleanupTimer?.Dispose();
            _timerCallbackDone.WaitOne(5000);
            
            _configWatcher?.Dispose();
            _fileWatcher?.Stop();
            _plcCommunicator?.Disconnect();
            (_plcCommunicator as IDisposable)?.Dispose();
            
            _timerCallbackDone.Dispose();
            _exitEvent.Dispose();
        }
    }
}