using System.IO;
using System.Text.Json;
using SMBApp.Models;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for loading and managing application configuration
    /// </summary>
    public class ConfigurationService
    {
        private const string ConfigFileName = "appsettings.json";
        private AppSettings? _appSettings;

        /// <summary>
        /// Loads the application settings from appsettings.json
        /// </summary>
        public AppSettings LoadConfiguration()
        {
            if (_appSettings != null)
            {
                return _appSettings;
            }

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Configuration file not found: {configPath}");
                }

                string jsonContent = File.ReadAllText(configPath);
                _appSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return _appSettings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all network configurations
        /// </summary>
        public List<NetworkConfiguration> GetNetworkConfigurations()
        {
            var settings = LoadConfiguration();
            return settings.NetworkConfigurations;
        }
    }
}