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
        /// Gets the full path to the configuration file
        /// </summary>
        private string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

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
                string configPath = ConfigFilePath;

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

        /// <summary>
        /// Gets the deployment settings
        /// </summary>
        public DeploymentSettings GetDeploymentSettings()
        {
            var settings = LoadConfiguration();
            return settings.DeploymentSettings;
        }

        /// <summary>
        /// Saves the deployment settings to appsettings.json
        /// </summary>
        public void SaveDeploymentSettings(DeploymentSettings deploymentSettings)
        {
            try
            {
                var settings = LoadConfiguration();
                settings.DeploymentSettings = deploymentSettings;

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null
                };

                string jsonContent = JsonSerializer.Serialize(settings, jsonOptions);
                File.WriteAllText(ConfigFilePath, jsonContent);

                // Reset cached settings so next load picks up changes
                _appSettings = null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save deployment settings: {ex.Message}", ex);
            }
        }
    }
}