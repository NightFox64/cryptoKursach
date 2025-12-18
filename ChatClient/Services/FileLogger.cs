using System;
using System.IO;
using System.Threading;

namespace ChatClient.Services
{
    public static class FileLogger
    {
        private static string? _logFilePath;
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;
            
            try
            {
                // Try to find project root by going up from bin directory
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"[FileLogger] Base directory: {baseDir}");
                
                // Go up from bin/Debug/net10.0-windows to project root
                var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
                
                if (projectRoot != null && Directory.Exists(projectRoot))
                {
                    _logFilePath = Path.Combine(projectRoot, "chat_client_logs.txt");
                }
                else
                {
                    // Fallback: use base directory
                    _logFilePath = Path.Combine(baseDir, "chat_client_logs.txt");
                }
                
                Console.WriteLine($"[FileLogger] Log file path: {_logFilePath}");
                
                // Clear old log file
                File.WriteAllText(_logFilePath, $"=== Chat Client Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                
                _initialized = true;
                Log($"Log file initialized at: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileLogger] Failed to initialize log file: {ex.Message}");
                Console.WriteLine($"[FileLogger] Stack trace: {ex.StackTrace}");
            }
        }

        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            
            // Write to console
            Console.WriteLine(logMessage);
            
            // Write to file
            if (_logFilePath != null && _initialized)
            {
                try
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileLogger] Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}
