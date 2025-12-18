using System;
using System.IO;

namespace ChatServer.Services
{
    public static class FileLogger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "chat_server_logs.txt");
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                var fullPath = Path.GetFullPath(LogFilePath);
                
                // Clear old log file
                File.WriteAllText(fullPath, $"=== Chat Server Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                
                _initialized = true;
                Log($"Log file initialized at: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            
            // Write to console
            Console.WriteLine(logMessage);
            
            // Write to file
            try
            {
                lock (_lockObject)
                {
                    var fullPath = Path.GetFullPath(LogFilePath);
                    File.AppendAllText(fullPath, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }
}
