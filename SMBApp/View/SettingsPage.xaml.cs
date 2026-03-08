using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SMBApp.Models;
using SMBApp.Services;

namespace SMBApp.View
{
    /// <summary>
    /// Settings page for managing AppSettings (network configurations and deployment sections) via UI.
    /// </summary>
    public partial class SettingsPage : Page
    {
        // Shared singleton — same instance as MainWindow, so BuildNavBar reads fresh data after save
        private readonly ConfigurationService _configurationService = ConfigurationService.Instance;
        private List<NetworkConfiguration> _configurations = new();
        private bool _isEditPasswordVisible = false;
        private bool _suppressSelectionChanged = false;

        private ObservableCollection<NavigationItem> _navItems = new();

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfigurations();
            LoadNavItems();
        }

        #region Configuration List

        private void LoadConfigurations()
        {
            _configurations = _configurationService.GetNetworkConfigurations();
            _suppressSelectionChanged = true;
            ConfigListBox.ItemsSource = null;
            ConfigListBox.ItemsSource = _configurations;
            _suppressSelectionChanged = false;

            if (_configurations.Count > 0)
                ConfigListBox.SelectedIndex = 0;
            else
                ClearEditPanel();

            SettingsStatusTextBlock.Text = $"Loaded {_configurations.Count} configuration(s)";
        }

        private void ConfigListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;

            if (ConfigListBox.SelectedItem is NetworkConfiguration config)
            {
                LoadConfigIntoEditPanel(config);
                EditPanel.IsEnabled = true;
            }
            else
            {
                ClearEditPanel();
                EditPanel.IsEnabled = false;
            }
        }

        private void AddConfigButton_Click(object sender, RoutedEventArgs e)
        {
            string baseName = "New Configuration";
            string name = baseName;
            int counter = 1;
            while (_configurations.Any(c => c.Name == name))
                name = $"{baseName} {counter++}";

            var newConfig = new NetworkConfiguration
            {
                Name = name,
                DeploymentSettings = new ConfigurationDeploymentSettings
                {
                    Sections = ConfigurationService.GetDefaultSections()
                }
            };

            _configurations.Add(newConfig);
            _configurationService.SaveNetworkConfiguration(newConfig);

            _suppressSelectionChanged = true;
            ConfigListBox.ItemsSource = null;
            ConfigListBox.ItemsSource = _configurations;
            _suppressSelectionChanged = false;

            ConfigListBox.SelectedItem = newConfig;
            SettingsStatusTextBlock.Text = $"Added new configuration: {name}";
        }

        private void DeleteConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigListBox.SelectedItem is not NetworkConfiguration config) return;

            var result = MessageBox.Show($"Delete configuration '{config.Name}'?\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _configurationService.RemoveNetworkConfiguration(config.Name);
            _configurations.Remove(config);

            _suppressSelectionChanged = true;
            ConfigListBox.ItemsSource = null;
            ConfigListBox.ItemsSource = _configurations;
            _suppressSelectionChanged = false;

            if (_configurations.Count > 0)
                ConfigListBox.SelectedIndex = 0;
            else
                ClearEditPanel();

            SettingsStatusTextBlock.Text = $"Deleted configuration: {config.Name}";
        }

        private void DuplicateConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigListBox.SelectedItem is not NetworkConfiguration source) return;

            string baseName = $"{source.Name} (Copy)";
            string name = baseName;
            int counter = 1;
            while (_configurations.Any(c => c.Name == name))
                name = $"{baseName} {counter++}";

            var duplicate = new NetworkConfiguration
            {
                Name = name,
                NetworkPath = source.NetworkPath,
                Username = source.Username,
                Password = source.Password,
                DeploymentSettings = new ConfigurationDeploymentSettings
                {
                    Sections = source.DeploymentSettings.Sections.Select(s => new DeploymentSectionConfig
                    {
                        SectionKey = s.SectionKey,
                        SectionTitle = s.SectionTitle,
                        ParentSourceFolder = s.ParentSourceFolder,
                        SelectedPaths = new List<string>(s.SelectedPaths),
                        IISWebsiteName = s.IISWebsiteName,
                        EnvFileName = s.EnvFileName
                    }).ToList()
                }
            };

            _configurations.Add(duplicate);
            _configurationService.SaveNetworkConfiguration(duplicate);

            _suppressSelectionChanged = true;
            ConfigListBox.ItemsSource = null;
            ConfigListBox.ItemsSource = _configurations;
            _suppressSelectionChanged = false;

            ConfigListBox.SelectedItem = duplicate;
            SettingsStatusTextBlock.Text = $"Duplicated as: {name}";
        }

        #endregion

        #region Edit Panel

        private void LoadConfigIntoEditPanel(NetworkConfiguration config)
        {
            EditNameTextBox.Text = config.Name;
            EditNetworkPathTextBox.Text = config.NetworkPath;
            EditUsernameTextBox.Text = config.Username;

            if (_isEditPasswordVisible)
                EditPasswordTextBox.Text = config.Password;
            else
                EditPasswordBox.Password = config.Password;

            LoadSectionTabs(config);
        }

        private void ClearEditPanel()
        {
            EditNameTextBox.Text = string.Empty;
            EditNetworkPathTextBox.Text = string.Empty;
            EditUsernameTextBox.Text = string.Empty;
            EditPasswordBox.Password = string.Empty;
            EditPasswordTextBox.Text = string.Empty;
            SectionsTabControl.Items.Clear();
            EditPanel.IsEnabled = false;
        }

        private void ToggleEditPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditPasswordVisible)
            {
                EditPasswordBox.Password = EditPasswordTextBox.Text;
                EditPasswordBox.Visibility = Visibility.Visible;
                EditPasswordTextBox.Visibility = Visibility.Collapsed;
                EditEyeIcon.Text = "👁";
                _isEditPasswordVisible = false;
            }
            else
            {
                EditPasswordTextBox.Text = EditPasswordBox.Password;
                EditPasswordBox.Visibility = Visibility.Collapsed;
                EditPasswordTextBox.Visibility = Visibility.Visible;
                EditEyeIcon.Text = "🙈";
                _isEditPasswordVisible = true;
            }
        }

        #endregion

        #region Deployment Sections

        private void LoadSectionTabs(NetworkConfiguration config)
        {
            SectionsTabControl.Items.Clear();

            var sections = config.DeploymentSettings.Sections;
            if (sections.Count == 0)
            {
                sections = ConfigurationService.GetDefaultSections();
                config.DeploymentSettings.Sections = sections;
            }

            foreach (var section in sections)
                AddSectionEditTab(section);

            if (SectionsTabControl.Items.Count > 0)
                SectionsTabControl.SelectedIndex = 0;
        }

        private void AddSectionEditTab(DeploymentSectionConfig section)
        {
            var panel = new StackPanel { Margin = new Thickness(10) };

            panel.Children.Add(CreateLabeledTextBox("Section Key:", section.SectionKey, "SectionKey"));
            panel.Children.Add(CreateLabeledTextBox("Section Title:", section.SectionTitle, "SectionTitle"));
            panel.Children.Add(CreateLabeledTextBox("IIS Website Name:", section.IISWebsiteName, "IISWebsiteName"));
            panel.Children.Add(CreateLabeledTextBox("Env/Config File:", section.EnvFileName, "EnvFileName"));

            var folderPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            folderPanel.Children.Add(new TextBlock
            {
                Text = "Parent Source Folder:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            folderPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(section.ParentSourceFolder) ? "(not set)" : section.ParentSourceFolder,
                Foreground = string.IsNullOrWhiteSpace(section.ParentSourceFolder)
                    ? System.Windows.Media.Brushes.Gray
                    : System.Windows.Media.Brushes.Black,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(folderPanel);

            panel.Children.Add(new TextBlock
            {
                Text = $"Selected Paths: {section.SelectedPaths?.Count ?? 0} item(s)",
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 4)
            });

            SectionsTabControl.Items.Add(new TabItem
            {
                Header = section.SectionKey,
                Content = new ScrollViewer
                {
                    Content = panel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                Tag = section.SectionKey
            });
        }

        private static StackPanel CreateLabeledTextBox(string label, string value, string tag)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(new TextBox
            {
                Text = value,
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = tag
            });
            return panel;
        }

        private void AddSectionToConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSectionDialog { Owner = Window.GetWindow(this) };

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var newSection = dialog.Result;

                foreach (TabItem existingTab in SectionsTabControl.Items)
                {
                    if (existingTab.Tag is string key && key == newSection.SectionKey)
                    {
                        MessageBox.Show($"A section with key '{newSection.SectionKey}' already exists.",
                            "Duplicate Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                AddSectionEditTab(newSection);
                SectionsTabControl.SelectedIndex = SectionsTabControl.Items.Count - 1;
                SettingsStatusTextBlock.Text = $"Added section: {newSection.SectionTitle}. Click 'Save Configuration' to persist.";
            }
        }

        private void RemoveSectionFromConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (SectionsTabControl.SelectedItem is not TabItem selectedTab) return;

            string sectionName = selectedTab.Header?.ToString() ?? "this section";
            var result = MessageBox.Show($"Remove section '{sectionName}' from this configuration?",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SectionsTabControl.Items.Remove(selectedTab);
                SettingsStatusTextBlock.Text = $"Removed section: {sectionName}. Click 'Save Configuration' to persist.";
            }
        }

        #endregion

        #region Navigation Tab Settings

        private void LoadNavItems()
        {
            var items = _configurationService.GetNavigationItems();
            _navItems = new ObservableCollection<NavigationItem>(items);
            NavItemsListBox.ItemsSource = _navItems;

            if (_navItems.Count > 0)
                NavItemsListBox.SelectedIndex = 0;
        }

        private void NavItemMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = NavItemsListBox.SelectedIndex;
            if (index <= 0) return;

            (_navItems[index], _navItems[index - 1]) = (_navItems[index - 1], _navItems[index]);
            NavItemsListBox.SelectedIndex = index - 1;
        }

        private void NavItemMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = NavItemsListBox.SelectedIndex;
            if (index < 0 || index >= _navItems.Count - 1) return;

            (_navItems[index], _navItems[index + 1]) = (_navItems[index + 1], _navItems[index]);
            NavItemsListBox.SelectedIndex = index + 1;
        }

        private void NavItemRename_Click(object sender, RoutedEventArgs e)
        {
            if (NavItemsListBox.SelectedItem is not NavigationItem selected) return;

            var dialog = new RenameNavItemDialog(selected.Label) { Owner = Window.GetWindow(this) };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewLabel))
            {
                selected.Label = dialog.NewLabel;
                NavItemsListBox.ItemsSource = null;
                NavItemsListBox.ItemsSource = _navItems;
                NavItemsListBox.SelectedItem = selected;
                SettingsStatusTextBlock.Text = $"Renamed tab to '{selected.Label}'. Click 'Save Nav Tabs' to apply.";
            }
        }

        private void SaveNavItemsButton_Click(object sender, RoutedEventArgs e)
        {
            // Persist to disk (also clears the shared cache on Instance)
            _configurationService.SaveNavigationItems(_navItems.ToList());

            // Rebuild nav bar — reads fresh data from the same Instance (cache already cleared)
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.BuildNavBar();

            SettingsStatusTextBlock.Text = "Navigation tabs saved and applied.";
            MessageBox.Show("Navigation tabs saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Save / Revert

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigListBox.SelectedItem is not NetworkConfiguration originalConfig) return;

            try
            {
                string originalName = originalConfig.Name;
                string newName = EditNameTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("Configuration name cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newName != originalName && _configurations.Any(c => c.Name == newName))
                {
                    MessageBox.Show($"A configuration named '{newName}' already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                originalConfig.Name = newName;
                originalConfig.NetworkPath = EditNetworkPathTextBox.Text.Trim();
                originalConfig.Username = EditUsernameTextBox.Text.Trim();
                originalConfig.Password = _isEditPasswordVisible ? EditPasswordTextBox.Text : EditPasswordBox.Password;

                var sections = ReadSectionsFromTabs();
                originalConfig.DeploymentSettings.Sections = sections;
                SyncLegacyProperties(originalConfig, sections);

                if (newName != originalName)
                    _configurationService.RemoveNetworkConfiguration(originalName);

                _configurationService.SaveNetworkConfiguration(originalConfig);

                _suppressSelectionChanged = true;
                int selectedIndex = ConfigListBox.SelectedIndex;
                ConfigListBox.ItemsSource = null;
                ConfigListBox.ItemsSource = _configurations;
                ConfigListBox.SelectedIndex = selectedIndex;
                _suppressSelectionChanged = false;

                SettingsStatusTextBlock.Text = $"Saved configuration: {newName}";
                MessageBox.Show($"Configuration '{newName}' saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RevertChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigListBox.SelectedItem is NetworkConfiguration config)
            {
                LoadConfigIntoEditPanel(config);
                SettingsStatusTextBlock.Text = "Changes reverted";
            }
        }

        private List<DeploymentSectionConfig> ReadSectionsFromTabs()
        {
            var sections = new List<DeploymentSectionConfig>();

            NetworkConfiguration? currentConfig = ConfigListBox.SelectedItem as NetworkConfiguration;
            var existingSections = currentConfig?.DeploymentSettings.Sections ?? new List<DeploymentSectionConfig>();

            foreach (TabItem tab in SectionsTabControl.Items)
            {
                if (tab.Content is ScrollViewer sv && sv.Content is StackPanel panel)
                {
                    string sectionKey = GetTextBoxValue(panel, "SectionKey");
                    string sectionTitle = GetTextBoxValue(panel, "SectionTitle");
                    string iisWebsiteName = GetTextBoxValue(panel, "IISWebsiteName");
                    string envFileName = GetTextBoxValue(panel, "EnvFileName");

                    var existing = existingSections.FirstOrDefault(s => s.SectionKey == (tab.Tag as string ?? sectionKey));

                    sections.Add(new DeploymentSectionConfig
                    {
                        SectionKey = sectionKey,
                        SectionTitle = sectionTitle,
                        ParentSourceFolder = existing?.ParentSourceFolder ?? string.Empty,
                        SelectedPaths = existing?.SelectedPaths ?? new List<string>(),
                        IISWebsiteName = iisWebsiteName,
                        EnvFileName = envFileName
                    });
                }
            }

            return sections;
        }

        private static string GetTextBoxValue(StackPanel panel, string tag)
        {
            foreach (var child in panel.Children)
            {
                if (child is StackPanel sp)
                {
                    foreach (var innerChild in sp.Children)
                    {
                        if (innerChild is TextBox tb && tb.Tag is string t && t == tag)
                            return tb.Text.Trim();
                    }
                }
            }
            return string.Empty;
        }

        private static void SyncLegacyProperties(NetworkConfiguration config, List<DeploymentSectionConfig> sections)
        {
            var frontend = sections.FirstOrDefault(s => s.SectionKey == "Frontend");
            if (frontend != null)
                config.DeploymentSettings.Frontend = new ConfigurationSectionPaths
                {
                    ParentSourceFolder = frontend.ParentSourceFolder,
                    SelectedPaths = frontend.SelectedPaths,
                    IISWebsiteName = frontend.IISWebsiteName
                };

            var api = sections.FirstOrDefault(s => s.SectionKey == "Api");
            if (api != null)
                config.DeploymentSettings.Api = new ConfigurationSectionPaths
                {
                    ParentSourceFolder = api.ParentSourceFolder,
                    SelectedPaths = api.SelectedPaths,
                    IISWebsiteName = api.IISWebsiteName
                };
        }

        #endregion
    }
}
