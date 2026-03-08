using System.IO;
using System.Text.Json;
using SMBApp.Models;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for loading and managing application configuration.
    /// A shared singleton instance is exposed via <see cref="Instance"/> so all
    /// consumers share one cache — ensuring <see cref="SaveSettings"/> cache
    /// invalidation is visible everywhere (e.g. MainWindow + SettingsPage).
    /// </summary>
    public class ConfigurationService
    {
        /// <summary>
        /// Shared singleton instance. Use this everywhere instead of <c>new ConfigurationService()</c>.
        /// </summary>
        public static readonly ConfigurationService Instance = new();

        private const string ConfigFileName = "appsettings.json";
        private AppSettings? _appSettings;

        private string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        /// <summary>
        /// Default navigation items used when none are persisted yet.
        /// </summary>
        public static List<NavigationItem> GetDefaultNavigationItems() =>
        [
            new() { PageKey = "SMBPage",               Label = "SMB Connection",     IsVisible = true },
            new() { PageKey = "DynamicDeploymentPage", Label = "Configurable Deploy", IsVisible = true },
            new() { PageKey = "SettingsPage",          Label = "⚙ Settings",          IsVisible = true },
        ];

        /// <summary>
        /// Loads the application settings from appsettings.json (cached after first load).
        /// </summary>
        public AppSettings LoadConfiguration()
        {
            if (_appSettings != null)
                return _appSettings;

            try
            {
                if (!File.Exists(ConfigFilePath))
                    throw new FileNotFoundException($"Configuration file not found: {ConfigFilePath}");

                string jsonContent = File.ReadAllText(ConfigFilePath);
                _appSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AppSettings();

                MigrateToSections(_appSettings);

                if (_appSettings.NavigationItems.Count == 0)
                    _appSettings.NavigationItems = GetDefaultNavigationItems();

                return _appSettings;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
            }
        }

        public List<NetworkConfiguration> GetNetworkConfigurations()
            => LoadConfiguration().NetworkConfigurations;

        public DeploymentSettings GetDeploymentSettings()
            => LoadConfiguration().DeploymentSettings;

        public AppSettings GetAppSettings()
            => LoadConfiguration();

        public List<NavigationItem> GetNavigationItems()
            => LoadConfiguration().NavigationItems;

        public void SaveNetworkConfiguration(NetworkConfiguration updatedConfig)
        {
            try
            {
                var settings = LoadConfiguration();
                var existing = settings.NetworkConfigurations.FirstOrDefault(c => c.Name == updatedConfig.Name);

                if (existing != null)
                {
                    existing.NetworkPath = updatedConfig.NetworkPath;
                    existing.Username = updatedConfig.Username;
                    existing.Password = updatedConfig.Password;
                    existing.DeploymentSettings = updatedConfig.DeploymentSettings;
                }
                else
                {
                    settings.NetworkConfigurations.Add(updatedConfig);
                }

                SaveSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save network configuration: {ex.Message}", ex);
            }
        }

        public void RemoveNetworkConfiguration(string configName)
        {
            try
            {
                var settings = LoadConfiguration();
                settings.NetworkConfigurations.RemoveAll(c => c.Name == configName);
                SaveSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove network configuration: {ex.Message}", ex);
            }
        }

        public void SaveDeploymentSettings(DeploymentSettings deploymentSettings)
        {
            try
            {
                var settings = LoadConfiguration();
                settings.DeploymentSettings = deploymentSettings;
                SaveSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save deployment settings: {ex.Message}", ex);
            }
        }

        public void SaveNavigationItems(List<NavigationItem> items)
        {
            try
            {
                var settings = LoadConfiguration();
                settings.NavigationItems = items;
                SaveSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save navigation items: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Persists all settings to disk and clears the shared cache.
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            };

            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(settings, jsonOptions));

            // Invalidate shared cache so the next call re-reads from disk
            _appSettings = null;
        }

        public static List<DeploymentSectionConfig> GetDefaultSections() =>
        [
            new() { SectionKey = "Frontend", SectionTitle = "Frontend Section", IISWebsiteName = "", EnvFileName = ".env" },
            new() { SectionKey = "Api",      SectionTitle = "API Section",      IISWebsiteName = "", EnvFileName = "appsettings.json" }
        ];

        private void MigrateToSections(AppSettings settings)
        {
            foreach (var config in settings.NetworkConfigurations)
            {
                if (config.DeploymentSettings.Sections.Count > 0)
                    continue;

                var ds = config.DeploymentSettings;
                var globalDs = settings.DeploymentSettings;

                bool hasFrontend = !string.IsNullOrEmpty(ds.Frontend?.IISWebsiteName)
                                   || !string.IsNullOrEmpty(ds.Frontend?.ParentSourceFolder);
                bool hasApi = !string.IsNullOrEmpty(ds.Api?.IISWebsiteName)
                              || !string.IsNullOrEmpty(ds.Api?.ParentSourceFolder);

                if (hasFrontend)
                    ds.Sections.Add(new DeploymentSectionConfig
                    {
                        SectionKey = "Frontend", SectionTitle = "Frontend Section",
                        ParentSourceFolder = ds.Frontend?.ParentSourceFolder ?? string.Empty,
                        SelectedPaths = ds.Frontend?.SelectedPaths ?? new(),
                        IISWebsiteName = ds.Frontend?.IISWebsiteName ?? string.Empty,
                        EnvFileName = globalDs?.Frontend?.EnvFileName ?? ".env"
                    });

                if (hasApi)
                    ds.Sections.Add(new DeploymentSectionConfig
                    {
                        SectionKey = "Api", SectionTitle = "API Section",
                        ParentSourceFolder = ds.Api?.ParentSourceFolder ?? string.Empty,
                        SelectedPaths = ds.Api?.SelectedPaths ?? new(),
                        IISWebsiteName = ds.Api?.IISWebsiteName ?? string.Empty,
                        EnvFileName = globalDs?.Api?.EnvFileName ?? "appsettings.json"
                    });

                if (ds.Sections.Count == 0)
                    ds.Sections.AddRange(GetDefaultSections());
            }
        }
    }
}