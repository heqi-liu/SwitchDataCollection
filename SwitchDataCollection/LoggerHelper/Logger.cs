using log4net;
using System;
using System.IO;

namespace SwitchDataCollection.LoggerHelper
{
    public static class Logger
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Logger));

        static Logger()
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LoggerHelper", "log4net.config");
            if (File.Exists(configPath))
                log4net.Config.XmlConfigurator.Configure(new FileInfo(configPath));
            else
            {
                string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
                if (File.Exists(fallbackPath))
                    log4net.Config.XmlConfigurator.Configure(new FileInfo(fallbackPath));
            }
        }

        public static void Debug(string message) => _log.Debug(message);
        public static void Info(string message) => _log.Info(message);
        public static void Warning(string message) => _log.Warn(message);
        public static void Error(string message) => _log.Error(message);
        public static void Error(string message, Exception ex) => _log.Error(message, ex);

        public static void CleanupOldLogs(int retentionDays)
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDir)) return;

                DateTime cutoffDate = DateTime.Now.AddDays(-retentionDays);
                int deletedCount = 0;

                foreach (var dir in Directory.GetDirectories(logDir))
                {
                    string dirName = Path.GetFileName(dir);
                    if (DateTime.TryParse(dirName, out DateTime dirDate))
                    {
                        if (dirDate < cutoffDate)
                        {
                            Directory.Delete(dir, true);
                            deletedCount++;
                            Info($"已删除过期日志目录: {dirName}");
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    Info($"共删除 {deletedCount} 个过期日志目录");
                }
            }
            catch (Exception ex)
            {
                Error("清理过期日志失败", ex);
            }
        }
    }
}
