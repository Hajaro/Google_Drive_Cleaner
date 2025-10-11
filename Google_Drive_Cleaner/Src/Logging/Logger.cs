using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoogleDriveFileRemover.Src.Utils;

namespace GoogleDriveFileRemover.Src.Logging
{
    internal static class Logger
    {
        public static void InitializeSeriLog(IConfiguration configuration, string programName)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var logDirectory = configuration["LoggerSettings:Folder"] ?? $"C:\\Logs\\{programName}";
            var jsonLogPath = Path.Combine(logDirectory, "warnings.json");
            var dailyTxtLogPath = Path.Combine(logDirectory, "all_logs-.txt");
            FileUtils.EnsurePathExists(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    path: jsonLogPath,
                    restrictedToMinimumLevel: LogEventLevel.Warning
                )
                .WriteTo.File(
                    path: dailyTxtLogPath,
                    rollingInterval: RollingInterval.Day
                )
                .CreateLogger();
        }

        public static void LogError(string message)
        {
            Log.Error(message);
        }

        public static void LogError(string message, Exception ex)
        {
            Log.Error(ex, message);
        }

        public static void LogInformation(string message)
        {
            Log.Information(message);
        }

        public static void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public static void LogFatal(string message)
        {
            Log.Fatal(message);
        }

        public static void LogDebug(string message)
        {
            Log.Debug(message);
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
