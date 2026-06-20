using System;
using System.IO;

namespace GamepadEmulator.Core
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.txt");
        private static readonly object _lock = new object();

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                WriteLog("ERROR", $"{message} | Exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            else
            {
                WriteLog("ERROR", message);
            }
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logEntry);
                }
            }
            catch
            {
                // Fallback silencioso caso não tenha permissão de escrita, mas em produção o log é vital
            }
        }
    }
}
