using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ChatServer.Middleware
{
    public class ExceptionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _logFilePath;

        public ExceptionLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
            // Ensure the logs directory exists
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            _logFilePath = Path.Combine(logDirectory, "server_exceptions.log");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var logMessage = @$"
============================================================
Timestamp: {DateTime.UtcNow:o}
Request: {context.Request.Method} {context.Request.Path}
------------------------------------------------------------
Exception:
{ex}
============================================================
";
                await File.AppendAllTextAsync(_logFilePath, logMessage);

                // Re-throw the exception to ensure the original error handling pipeline is not disrupted
                throw;
            }
        }
    }
}
