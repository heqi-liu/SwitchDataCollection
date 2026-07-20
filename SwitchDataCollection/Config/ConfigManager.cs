using Newtonsoft.Json;
using SwitchDataCollection.LoggerHelper;
using System;
using System.IO;

namespace SwitchDataCollection.Config
{
    public class Configuration
    {
        public DataConfig DataConfig { get; set; }
        public PlcCommunicationConfig PlcCommunication { get; set; }
    }

    public class DataConfig
    {
        public string TargetFolderPath { get; set; }
        public int ReadRows { get; set; }
        public int[] ColumnIndices { get; set; }
        public int QueueMaxSize { get; set; }
        public string TargetFileInclude { get; set; }
        public string TargetFileNoInclude { get; set; }
        public int LogRetentionDays { get; set; }
    }

    public class PlcCommunicationConfig
    {
        public bool Enabled { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int WriteIntervalSeconds { get; set; }
        public string StartAddress { get; set; }
        public int WriteWordLength { get; set; }
    }

    public static class ConfigManager
    {
        private static Configuration _config;
        private static readonly object _lock = new object();
        private static DateTime _lastModifyTime;

        public static Configuration GetConfig()
        {
            if (_config == null)
            {
                lock (_lock)
                {
                    if (_config == null) LoadConfig();
                }
            }
            return _config;
        }

        public static bool CheckConfigChanged()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.json");
            if (!File.Exists(path)) return false;
            return File.GetLastWriteTime(path) > _lastModifyTime;
        }

        public static void ReloadConfig()
        {
            lock (_lock)
            {
                try
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                    string path = Path.Combine(dir, "config.json");

                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (!File.Exists(path)) CreateDefaultConfig(path);

                    var newConfig = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(path));
                    newConfig.DataConfig.ColumnIndices = newConfig.DataConfig.ColumnIndices ?? new int[0];
                    ValidateConfig(newConfig);
                    _config = newConfig;
                    _lastModifyTime = File.GetLastWriteTime(path);
                    Logger.Info($"配置文件重新加载成功");
                }
                catch (JsonException ex)
                {
                    Logger.Error("配置文件格式错误，保持原有配置", ex);
                    throw new InvalidOperationException("配置文件格式错误", ex);
                }
                catch (Exception ex)
                {
                    Logger.Error("读取配置文件失败，保持原有配置", ex);
                    throw new InvalidOperationException("读取配置文件失败", ex);
                }
            }
        }

        private static void ValidateConfig(Configuration config)
        {
            if (config.DataConfig == null)
                throw new InvalidOperationException("DataConfig 不能为空");
            if (string.IsNullOrWhiteSpace(config.DataConfig.TargetFolderPath))
                throw new InvalidOperationException("TargetFolderPath 不能为空");
            if (config.DataConfig.ReadRows <= 0)
                throw new InvalidOperationException("ReadRows 必须大于0");
            if (config.DataConfig.ColumnIndices == null)
                config.DataConfig.ColumnIndices = new int[0];
            if (config.DataConfig.QueueMaxSize <= 0)
                config.DataConfig.QueueMaxSize = 1000;
            if (config.DataConfig.LogRetentionDays <= 0)
                config.DataConfig.LogRetentionDays = 14;

            if (config.PlcCommunication != null)
            {
                if (config.PlcCommunication.WriteIntervalSeconds <= 0)
                    config.PlcCommunication.WriteIntervalSeconds = 5;
                if (string.IsNullOrWhiteSpace(config.PlcCommunication.StartAddress))
                    config.PlcCommunication.StartAddress = "D100";
                if (config.PlcCommunication.WriteWordLength <= 0)
                    config.PlcCommunication.WriteWordLength = 50;
            }
        }

        private static void LoadConfig()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            string path = Path.Combine(dir, "config.json");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path)) CreateDefaultConfig(path);

            try
            {
                var newConfig = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(path));
                _lastModifyTime = File.GetLastWriteTime(path);
                ValidateConfig(newConfig);
                _config = newConfig;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("配置文件格式错误", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("读取配置文件失败", ex);
            }
        }

        private static void CreateDefaultConfig(string path)
        {
            var config = new Configuration
            {
                DataConfig = new DataConfig
                {
                    TargetFolderPath = "./Data",
                    ReadRows = 100,
                    ColumnIndices = new int[0],
                    QueueMaxSize = 1000
                },
                PlcCommunication = new PlcCommunicationConfig
                {
                    Enabled = false,
                    IpAddress = "192.168.0.1",
                    Port = 9600,
                    WriteIntervalSeconds = 5,
                    StartAddress = "D100",
                    WriteWordLength = 50
                }
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }
    }
}
