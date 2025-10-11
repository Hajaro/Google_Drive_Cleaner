using GoogleDriveFileRemover.Src.Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveFileRemover.Src.Helpers
{
    internal class ConfigurationHelper
    {
        public static IConfiguration InitializeConfig(string? path = "./Resources/Settings", string? profile = "")
        {
            ArgumentNullException.ThrowIfNull(path);
            var fileName = string.IsNullOrEmpty(profile)
                ? "appsettings.json"
                : $"appsettings.{profile}.json";

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path, fileName);

            FileUtils.EnsurePathExists(fullPath);
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile(fullPath, optional: true, reloadOnChange: true);

                IConfiguration configuration = builder.Build();

                return configuration;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas inicjalizacji konfiguracji z {fullPath}: {ex.Message}", ex);
            }
        }

    }
}
