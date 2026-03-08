using System.IO;
using System.Windows;
using System.Windows.Controls;
using SMBApp.Models;
using SMBApp.Services;
using SMBApp.View.Controls;

namespace SMBApp.View
{
    /// <summary>
    /// A configurable deployment page that uses dynamic, tabbed DeploymentSectionControls.
    /// Users can add/remove sections per connection.
    /// </summary>
    public partial class DynamicDeploymentPage : Page
    {
        private readonly SMBService _smbService;
        private readonly ConfigurationService _configurationService;
        private readonly DeploymentService _deploymentService;
        private readonly IISService _iisService;

        private bool _isPasswordVisible = false;
        private bool _isConnected = false;

        public DynamicDeploymentPage()
        {
            InitializeComponent();
            _smbService = new SMBService();
            _configurationService = new ConfigurationService();
            _deploymentService = new DeploymentService();
            _iisService = new IISService();

            Loaded += DynamicDeploymentPage_Loaded;
        }

        private void DynamicDeploymentPage_Loaded(object sender, RoutedEventArgs e)
        {
            _deploymentService.EnsureFoldersExist();
            LoadNetworkConfigurations();
        }

        #region Connection Configuration

        private void LoadNetworkConfigurations()
        {
            try
            {
                var configurations = _configurationService.GetNetworkConfigurations();
                NetworkConfigComboBox.ItemsSource = configurations;

                if (configurations.Count > 0)
                    NetworkConfigComboBox.SelectedIndex = 0;

                StatusTextBlock.Text = $"Loaded {configurations.Count} saved configuration(s)";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to load configurations: {ex.Message}";
            }
        }

        private void NetworkConfigComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NetworkConfigComboBox.SelectedItem is NetworkConfiguration selectedConfig)
            {
                NetworkPathTextBox.Text = selectedConfig.NetworkPath;
                UsernameTextBox.Text = selectedConfig.Username;

                if (_isPasswordVisible)
                    PasswordTextBox.Text = selectedConfig.Password;
                else
                    PasswordBox.Password = selectedConfig.Password;

                LoadSectionsForConfiguration(selectedConfig);
                StatusTextBlock.Text = $"Loaded configuration: {selectedConfig.Name}";
            }
        }

        private void LoadSectionsForConfiguration(NetworkConfiguration config)
        {
            SectionsTabControl.Items.Clear();

            var sections = config.DeploymentSettings.Sections;
            if (sections.Count == 0)
            {
                sections = ConfigurationService.GetDefaultSections();
                config.DeploymentSettings.Sections = sections;
            }

            foreach (var section in sections)
            {
                AddSectionTab(section);
            }

            if (SectionsTabControl.Items.Count > 0)
                SectionsTabControl.SelectedIndex = 0;
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                EyeIcon.Text = "👁";
                _isPasswordVisible = false;
            }
            else
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                EyeIcon.Text = "🙈";
                _isPasswordVisible = true;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Connecting to SMB share...";

                string networkPath = NetworkPathTextBox.Text;
                string username = UsernameTextBox.Text;
                string password = GetPassword();

                if (string.IsNullOrWhiteSpace(networkPath) || string.IsNullOrWhiteSpace(username)
                    || string.IsNullOrWhiteSpace(password))
                {
                    StatusTextBlock.Text = "Please fill in all connection fields";
                    MessageBox.Show("Please fill in all connection fields (Network Path, Username, Password)",
                        "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() =>
                {
                    bool connected = _smbService.ConnectToShare(networkPath, username, password);
                    if (connected && !_smbService.TestConnection(networkPath))
                        throw new InvalidOperationException("Connected but path is not accessible");
                });

                _isConnected = true;
                StatusTextBlock.Text = $"Successfully connected to {networkPath}";

                // Update all section controls with connection info
                UpdateAllSectionsConnectionInfo();

                // Auto-load IIS websites
                await LoadIisWebsitesAsync();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                StatusTextBlock.Text = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect:\n{ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string networkPath = NetworkPathTextBox.Text;
                bool disconnected = _smbService.DisconnectFromShare(networkPath);

                if (disconnected)
                {
                    _isConnected = false;
                    StatusTextBlock.Text = "Disconnected successfully";
                    UpdateAllSectionsConnectionInfo();
                }
                else
                {
                    StatusTextBlock.Text = "Disconnect failed";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to disconnect:\n{ex.Message}", "Disconnect Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Section Management

        private void AddSectionTab(DeploymentSectionConfig config)
        {
            var control = new DeploymentSectionControl();
            control.Initialize(_deploymentService, _iisService);
            control.LoadSettings(config);

            // Wire events
            control.RefreshWebsitesRequested += async (s, _) => await LoadIisWebsitesAsync();
            control.SaveSettingsRequested += OnSectionSaveRequested;
            control.StatusMessageChanged += (s, msg) =>
                Dispatcher.Invoke(() => StatusTextBlock.Text = $"[{config.SectionKey}] {msg}");

            // Set connection info if already connected
            if (_isConnected)
            {
                string networkPath = NetworkPathTextBox.Text;
                string serverName = ExtractServerName(networkPath);
                string username = UsernameTextBox.Text;
                string password = GetPassword();
                control.SetConnectionInfo(true, networkPath, serverName, username, password);
            }

            var tab = new TabItem
            {
                Header = config.SectionTitle,
                Content = control,
                Tag = config.SectionKey
            };

            SectionsTabControl.Items.Add(tab);
        }

        private void AddSectionButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSectionDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var newConfig = dialog.Result;

                // Check for duplicate keys
                foreach (TabItem existingTab in SectionsTabControl.Items)
                {
                    if (existingTab.Tag is string key && key == newConfig.SectionKey)
                    {
                        MessageBox.Show($"A section with key '{newConfig.SectionKey}' already exists.",
                            "Duplicate Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                AddSectionTab(newConfig);
                SectionsTabControl.SelectedIndex = SectionsTabControl.Items.Count - 1;

                // Save to current configuration
                SaveAllSectionsToCurrentConfig();
                StatusTextBlock.Text = $"Added section: {newConfig.SectionTitle}";
            }
        }

        private void RemoveSectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SectionsTabControl.SelectedItem is not TabItem selectedTab) return;

            string sectionName = selectedTab.Header?.ToString() ?? "this section";
            var result = MessageBox.Show($"Remove section '{sectionName}'?",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SectionsTabControl.Items.Remove(selectedTab);
                SaveAllSectionsToCurrentConfig();
                StatusTextBlock.Text = $"Removed section: {sectionName}";
            }
        }

        #endregion

        #region Save / Load

        private void OnSectionSaveRequested(object? sender, DeploymentSectionConfig sectionConfig)
        {
            try
            {
                if (NetworkConfigComboBox.SelectedItem is not NetworkConfiguration selectedConfig)
                {
                    MessageBox.Show("Please select a network configuration first.",
                        "No Configuration Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update just this section in the config
                var sections = selectedConfig.DeploymentSettings.Sections;
                var existing = sections.FirstOrDefault(s => s.SectionKey == sectionConfig.SectionKey);
                if (existing != null)
                {
                    int idx = sections.IndexOf(existing);
                    sections[idx] = sectionConfig;
                }
                else
                {
                    sections.Add(sectionConfig);
                }

                // Also keep legacy properties in sync
                SyncLegacyProperties(selectedConfig, sections);

                _configurationService.SaveNetworkConfiguration(selectedConfig);
                StatusTextBlock.Text = $"Saved {sectionConfig.SectionKey} settings for: {selectedConfig.Name}";
                MessageBox.Show($"Settings saved for section '{sectionConfig.SectionTitle}' in configuration '{selectedConfig.Name}'.",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings:\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAllSectionsToCurrentConfig()
        {
            if (NetworkConfigComboBox.SelectedItem is not NetworkConfiguration selectedConfig) return;

            try
            {
                var sections = new List<DeploymentSectionConfig>();
                foreach (TabItem tab in SectionsTabControl.Items)
                {
                    if (tab.Content is DeploymentSectionControl control)
                    {
                        sections.Add(control.GetCurrentSettings());
                    }
                }

                selectedConfig.DeploymentSettings.Sections = sections;
                SyncLegacyProperties(selectedConfig, sections);
                _configurationService.SaveNetworkConfiguration(selectedConfig);
            }
            catch
            {
                // Silent save failure for structural changes; user can save explicitly
            }
        }

        /// <summary>
        /// Keeps the legacy Frontend/Api properties in sync with the new Sections list
        /// for backwards compatibility with the existing DeploymentPage
        /// </summary>
        private static void SyncLegacyProperties(NetworkConfiguration config, List<DeploymentSectionConfig> sections)
        {
            var frontend = sections.FirstOrDefault(s => s.SectionKey == "Frontend");
            if (frontend != null)
            {
                config.DeploymentSettings.Frontend = new ConfigurationSectionPaths
                {
                    ParentSourceFolder = frontend.ParentSourceFolder,
                    SelectedPaths = frontend.SelectedPaths,
                    IISWebsiteName = frontend.IISWebsiteName
                };
            }

            var api = sections.FirstOrDefault(s => s.SectionKey == "Api");
            if (api != null)
            {
                config.DeploymentSettings.Api = new ConfigurationSectionPaths
                {
                    ParentSourceFolder = api.ParentSourceFolder,
                    SelectedPaths = api.SelectedPaths,
                    IISWebsiteName = api.IISWebsiteName
                };
            }
        }

        #endregion

        #region IIS Websites

        private async Task LoadIisWebsitesAsync()
        {
            string networkPath = NetworkPathTextBox.Text;
            string serverName = ExtractServerName(networkPath);
            string username = UsernameTextBox.Text;
            string password = GetPassword();
            _iisService.SetCredentials(username, password);

            StatusTextBlock.Text = "Loading IIS websites from server...";

            try
            {
                var websites = await _iisService.GetWebsitesAsync(serverName);

                // Distribute to all section controls
                foreach (TabItem tab in SectionsTabControl.Items)
                {
                    if (tab.Content is DeploymentSectionControl control)
                    {
                        control.SetWebsiteList(websites);
                    }
                }

                StatusTextBlock.Text = $"Loaded {websites.Count} IIS website(s) from {serverName}";

                // Refresh status for each section
                foreach (TabItem tab in SectionsTabControl.Items)
                {
                    if (tab.Content is DeploymentSectionControl control)
                    {
                        await control.RefreshWebsiteStatusAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Could not load IIS websites: {ex.Message}";
            }
        }

        #endregion

        #region Helpers

        private void UpdateAllSectionsConnectionInfo()
        {
            string networkPath = NetworkPathTextBox.Text;
            string serverName = ExtractServerName(networkPath);
            string username = UsernameTextBox.Text;
            string password = GetPassword();

            foreach (TabItem tab in SectionsTabControl.Items)
            {
                if (tab.Content is DeploymentSectionControl control)
                {
                    control.SetConnectionInfo(_isConnected, networkPath, serverName, username, password);
                }
            }
        }

        private string GetPassword() =>
            _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

        private static string ExtractServerName(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath) || !uncPath.StartsWith(@"\\"))
                return uncPath;

            string withoutPrefix = uncPath[2..];
            int slashIndex = withoutPrefix.IndexOf('\\');
            return slashIndex > 0 ? withoutPrefix[..slashIndex] : withoutPrefix;
        }

        #endregion
    }
}
