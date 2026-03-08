using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SMBApp.Models;
using SMBApp.Services;

namespace SMBApp.View.Controls
{
    /// <summary>
    /// Represents a selected file or folder item for display in the section
    /// </summary>
    public class SectionSelectedItemDisplay
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";
    }

    /// <summary>
    /// A reusable, self-contained deployment section control.
    /// Encapsulates IIS management, file selection, and full deployment
    /// pipeline (zip, transfer, deploy) for a single configurable section.
    /// </summary>
    public partial class DeploymentSectionControl : UserControl
    {
        private DeploymentService? _deploymentService;
        private IISService? _iisService;

        // Connection state (set by parent)
        private bool _isConnected;
        private string _networkPath = string.Empty;
        private string _serverName = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;

        // Section config
        private string _sectionKey = string.Empty;
        private string _envFileName = string.Empty;

        // File selection state
        private string _parentSourceFolder = string.Empty;
        private readonly List<string> _selectedPaths = new();

        // Deployment state
        private CancellationTokenSource? _cancellationTokenSource;
        private Stopwatch? _deploymentStopwatch;
        private bool _suppressWebsiteCascade = false;

        #region Events

        /// <summary>Raised when the control wants to refresh IIS websites for all sections</summary>
        public event EventHandler? RefreshWebsitesRequested;

        /// <summary>Raised when the user clicks Save Settings</summary>
        public event EventHandler<DeploymentSectionConfig>? SaveSettingsRequested;

        /// <summary>Raised when a status message should appear on the parent page</summary>
        public event EventHandler<string>? StatusMessageChanged;

        #endregion

        #region Public Properties

        public string SectionKey
        {
            get => _sectionKey;
            set => _sectionKey = value;
        }

        public string SectionTitle
        {
            get => SectionTitleTextBlock?.Text ?? string.Empty;
            set { if (SectionTitleTextBlock != null) SectionTitleTextBlock.Text = value; }
        }

        public string EnvFileName
        {
            get => _envFileName;
            set => _envFileName = value;
        }

        #endregion

        public DeploymentSectionControl()
        {
            InitializeComponent();
            SetIisControlsEnabled(false);
        }

        #region Initialization / Configuration

        /// <summary>
        /// Injects the required services from the parent page
        /// </summary>
        public void Initialize(DeploymentService deploymentService, IISService iisService)
        {
            _deploymentService = deploymentService;
            _iisService = iisService;
        }

        /// <summary>
        /// Called by the parent page whenever connection state changes
        /// </summary>
        public void SetConnectionInfo(bool isConnected, string networkPath, string serverName,
                                      string username, string password)
        {
            _isConnected = isConnected;
            _networkPath = networkPath;
            _serverName = serverName;
            _username = username;
            _password = password;

            if (!isConnected)
                SetIisControlsEnabled(false);
        }

        /// <summary>
        /// Populates the IIS website dropdown list
        /// </summary>
        public void SetWebsiteList(List<string> websites)
        {
            string current = GetIISWebsiteName();
            _suppressWebsiteCascade = true;
            IISWebsiteComboBox.ItemsSource = websites;
            IISWebsiteComboBox.Text = current;
            _suppressWebsiteCascade = false;
        }

        /// <summary>
        /// Loads saved settings into the control
        /// </summary>
        public void LoadSettings(DeploymentSectionConfig config)
        {
            _sectionKey = config.SectionKey;
            SectionTitleTextBlock.Text = config.SectionTitle;
            _envFileName = config.EnvFileName;

            if (!string.IsNullOrWhiteSpace(config.ParentSourceFolder))
            {
                _parentSourceFolder = config.ParentSourceFolder;
                ParentSourceFolderLabel.Text = _parentSourceFolder;
                ParentSourceFolderLabel.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                _parentSourceFolder = string.Empty;
                ParentSourceFolderLabel.Text = "(not set)";
                ParentSourceFolderLabel.Foreground = System.Windows.Media.Brushes.Gray;
            }

            _suppressWebsiteCascade = true;
            IISWebsiteComboBox.Text = config.IISWebsiteName;
            _suppressWebsiteCascade = false;

            _selectedPaths.Clear();
            if (config.SelectedPaths?.Count > 0)
                _selectedPaths.AddRange(config.SelectedPaths);
            RefreshSelectedItemsDisplay();
        }

        /// <summary>
        /// Returns the current state of this section as a config object
        /// </summary>
        public DeploymentSectionConfig GetCurrentSettings()
        {
            return new DeploymentSectionConfig
            {
                SectionKey = _sectionKey,
                SectionTitle = SectionTitleTextBlock.Text,
                ParentSourceFolder = _parentSourceFolder,
                SelectedPaths = new List<string>(_selectedPaths),
                IISWebsiteName = GetIISWebsiteName(),
                EnvFileName = _envFileName
            };
        }

        /// <summary>
        /// Refreshes the IIS status for the currently selected website
        /// </summary>
        public async Task RefreshWebsiteStatusAsync()
        {
            string websiteName = GetIISWebsiteName();
            if (!string.IsNullOrWhiteSpace(websiteName) && _isConnected)
                await ApplyWebsiteStatusToUiAsync(websiteName);
        }

        #endregion

        #region IIS Website Controls

        private string GetIISWebsiteName() =>
            IISWebsiteComboBox.SelectedItem as string
            ?? IISWebsiteComboBox.Text?.Trim()
            ?? string.Empty;

        private async void IISWebsiteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressWebsiteCascade || !_isConnected) return;

            if (IISWebsiteComboBox.SelectedItem is string websiteName && !string.IsNullOrWhiteSpace(websiteName))
                await ApplyWebsiteStatusToUiAsync(websiteName);
        }

        private void RefreshWebsitesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Please connect to the SMB share first.",
                    "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshWebsitesRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void StartWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteIisActionAsync(GetIISWebsiteName(), "Start Website",
                async (server, name) => await _iisService!.StartWebsiteAsync(server, name));
        }

        private async void StopWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteIisActionAsync(GetIISWebsiteName(), "Stop Website",
                async (server, name) => await _iisService!.StopWebsiteAsync(server, name));
        }

        private async void StartAppPoolButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteIisActionAsync(AppPoolNameTextBox.Text.Trim(), "Start App Pool",
                async (server, name) => await _iisService!.StartAppPoolAsync(server, name));
        }

        private async void StopAppPoolButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteIisActionAsync(AppPoolNameTextBox.Text.Trim(), "Stop App Pool",
                async (server, name) => await _iisService!.StopAppPoolAsync(server, name));
        }

        private async Task ExecuteIisActionAsync(string targetName, string actionName,
            Func<string, string, Task> action)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Please connect to the SMB share first.",
                    "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                MessageBox.Show($"Please enter a name before performing '{actionName}'.",
                    "Missing Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _iisService!.SetCredentials(_username, _password);
            SetIisControlsEnabled(false);
            UpdateSectionStatus($"{actionName}: '{targetName}'...");

            try
            {
                await action(_serverName, targetName);
                UpdateSectionStatus($"✅ {actionName} completed for '{targetName}'.");
            }
            catch (Exception ex)
            {
                UpdateSectionStatus($"❌ {actionName} failed: {ex.Message}");
                MessageBox.Show($"{actionName} failed:\n{ex.Message}",
                    "IIS Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await ApplyWebsiteStatusToUiAsync(GetIISWebsiteName());
        }

        private async Task ApplyWebsiteStatusToUiAsync(string websiteName)
        {
            if (_iisService == null || string.IsNullOrWhiteSpace(_serverName)) return;

            _iisService.SetCredentials(_username, _password);

            await Dispatcher.InvokeAsync(() =>
            {
                AppPoolNameTextBox.Text = "Loading...";
                SetIisControlsEnabled(false);
                SectionStatusTextBlock.Text = $"Fetching status for '{websiteName}'...";
            });

            try
            {
                IISWebsiteStatus? status = await _iisService.GetWebsiteStatusAsync(_serverName, websiteName);

                if (status is null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppPoolNameTextBox.Text = "(not found)";
                        SectionStatusTextBlock.Text = $"⚠️ Website '{websiteName}' not found on server.";
                        SetIisControlsEnabled(false);
                    });
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    AppPoolNameTextBox.Text = status.AppPoolName;
                    StartWebsiteButton.IsEnabled = !status.IsWebsiteStarted;
                    StopWebsiteButton.IsEnabled = status.IsWebsiteStarted;
                    StartAppPoolButton.IsEnabled = !status.IsAppPoolStarted;
                    StopAppPoolButton.IsEnabled = status.IsAppPoolStarted;

                    string siteState = status.IsWebsiteStarted ? "🟢 Started" : "🔴 Stopped";
                    string poolState = status.IsAppPoolStarted ? "🟢 Started" : "🔴 Stopped";
                    SectionStatusTextBlock.Text = $"Website: {siteState}  |  App Pool: {poolState}";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    AppPoolNameTextBox.Text = "(error)";
                    SectionStatusTextBlock.Text = $"❌ Failed to fetch status: {ex.Message}";
                    SetIisControlsEnabled(false);
                });
            }
        }

        private void SetIisControlsEnabled(bool enabled)
        {
            StartWebsiteButton.IsEnabled = enabled;
            StopWebsiteButton.IsEnabled = enabled;
            StartAppPoolButton.IsEnabled = enabled;
            StopAppPoolButton.IsEnabled = enabled;
        }

        #endregion

        #region Step 1: Select Files and Folders

        private void SelectParentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = $"Select Parent Source Folder for {_sectionKey}",
                InitialDirectory = Directory.Exists(_parentSourceFolder) ? _parentSourceFolder : string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                _parentSourceFolder = dialog.FolderName;
                ParentSourceFolderLabel.Text = _parentSourceFolder;
                ParentSourceFolderLabel.Foreground = System.Windows.Media.Brushes.Black;
                UpdateSectionStatus($"Parent folder set: {_parentSourceFolder}");
            }
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_parentSourceFolder) || !Directory.Exists(_parentSourceFolder))
            {
                MessageBox.Show("Please select a valid parent source folder first.",
                    "No Parent Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = $"Select Files to Deploy ({_sectionKey})",
                InitialDirectory = _parentSourceFolder,
                Filter = "All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!_selectedPaths.Contains(file))
                        _selectedPaths.Add(file);
                }

                RefreshSelectedItemsDisplay();
                UpdateSectionStatus($"{_selectedPaths.Count} item(s) selected");
            }
        }

        private void SelectFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_parentSourceFolder) || !Directory.Exists(_parentSourceFolder))
            {
                MessageBox.Show("Please select a valid parent source folder first.",
                    "No Parent Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var folderDialog = new OpenFolderDialog
            {
                Title = $"Select Folders to Deploy ({_sectionKey})",
                InitialDirectory = _parentSourceFolder,
                Multiselect = true
            };

            if (folderDialog.ShowDialog() == true)
            {
                foreach (var folder in folderDialog.FolderNames)
                {
                    if (!_selectedPaths.Contains(folder))
                        _selectedPaths.Add(folder);
                }

                RefreshSelectedItemsDisplay();
                UpdateSectionStatus($"{_selectedPaths.Count} item(s) selected");
            }
        }

        private void ClearSelectedItems_Click(object sender, RoutedEventArgs e)
        {
            _selectedPaths.Clear();
            RefreshSelectedItemsDisplay();
            UpdateSectionStatus("Selection cleared. Save Settings to persist the change.");
        }

        private void RemoveSelectedItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string path)
            {
                _selectedPaths.Remove(path);
                RefreshSelectedItemsDisplay();
                UpdateSectionStatus($"{_selectedPaths.Count} item(s) selected");
            }
        }

        private void RefreshSelectedItemsDisplay()
        {
            var displayItems = _selectedPaths.Select(p => new SectionSelectedItemDisplay
            {
                Name = Path.GetFileName(p),
                FullPath = p,
                Icon = Directory.Exists(p) ? "📁" : "📄"
            }).ToList();

            SelectedItemsControl.ItemsSource = null;
            SelectedItemsControl.ItemsSource = displayItems;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsRequested?.Invoke(this, GetCurrentSettings());
        }

        #endregion

        #region Steps 2-4: Execute Deployment

        private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("Please connect to the SMB share first.",
                    "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_deploymentService == null || _iisService == null)
            {
                MessageBox.Show("Services not initialized.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string destinationFolder = _networkPath;

            if (!Directory.Exists(destinationFolder))
            {
                MessageBox.Show($"Destination folder does not exist:\n{destinationFolder}",
                    "Invalid Destination", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_parentSourceFolder) || !Directory.Exists(_parentSourceFolder))
            {
                MessageBox.Show("Parent source folder is not set or does not exist.",
                    "Invalid Source", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedPaths.Count == 0)
            {
                MessageBox.Show("No files or folders selected for deployment.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var path in _selectedPaths)
            {
                string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_parentSourceFolder, path);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    MessageBox.Show($"Selected item not found:\n{fullPath}",
                        "Missing Item", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _deploymentService.SetCredentials(_username, _password);
            _iisService.SetCredentials(_username, _password);

            ResetChecklists();
            _deploymentStopwatch = Stopwatch.StartNew();

            _cancellationTokenSource = new CancellationTokenSource();
            ExecuteButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            MainProgressBar.Visibility = Visibility.Visible;
            MainProgressBar.IsIndeterminate = true;
            TimeElapsedTextBlock.Visibility = Visibility.Visible;

            var timerUpdateTask = UpdateTimerAsync(_deploymentStopwatch, _cancellationTokenSource.Token);

            string folderName = _deploymentService.GenerateSectionFolderName(_sectionKey);
            string zipFileName = folderName + ".zip";
            string iisWebsiteName = GetIISWebsiteName();

            try
            {
                var ct = _cancellationTokenSource.Token;

                // STEP 2
                UpdateSectionStatus("Step 2: Creating date subfolder...");
                string dateFolderPath = await Task.Run(() => _deploymentService.GetOrCreateDateSubFolder(), ct);
                await SetCheckboxAsync(Chk2_CreateDateFolder, true);

                UpdateSectionStatus("Step 2: Copying selected items...");
                string stagingFolder = Path.Combine(dateFolderPath, folderName);
                await _deploymentService.CopySelectedItemsAsync(_parentSourceFolder, _selectedPaths, stagingFolder, ct);
                await SetCheckboxAsync(Chk2_CopyItems, true);

                UpdateSectionStatus("Step 2: Validating copied items...");
                bool valid = await Task.Run(() =>
                    _deploymentService.ValidateCopiedItems(stagingFolder, _selectedPaths, _parentSourceFolder), ct);
                if (!valid)
                    throw new InvalidOperationException("Copied items validation failed. Some items are missing.");
                await SetCheckboxAsync(Chk2_ValidateCopy, true);

                UpdateSectionStatus("Step 2: Zipping folder...");
                await Dispatcher.InvokeAsync(() =>
                {
                    ZipProgressBar.Visibility = Visibility.Visible;
                    ZipProgressBar.Value = 0;
                    ZipProgressLabel.Visibility = Visibility.Visible;
                    ZipProgressLabel.Text = "Preparing to zip...";
                    MainProgressBar.IsIndeterminate = false;
                });

                var zipProgress = new Progress<(int Percent, string FileName)>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ZipProgressBar.Value = p.Percent;
                        ZipProgressLabel.Text = $"Zipping {p.Percent}% — {p.FileName}";
                        SectionStatusTextBlock.Text = $"Step 2: Zipping... {p.Percent}%";
                    });
                });

                string zipPath = await Task.Run(() => _deploymentService.ZipFolder(stagingFolder, zipProgress), ct);

                await Dispatcher.InvokeAsync(() =>
                {
                    ZipProgressBar.Visibility = Visibility.Collapsed;
                    ZipProgressLabel.Text = "✅ Zip complete";
                    MainProgressBar.IsIndeterminate = true;
                });
                await SetCheckboxAsync(Chk2_ZipFolder, true);

                // STEP 3
                UpdateSectionStatus("Step 3: Transferring zip to remote server...");
                await Dispatcher.InvokeAsync(() =>
                {
                    MainProgressBar.IsIndeterminate = false;
                    MainProgressBar.Value = 0;
                });

                var transferProgress = new Progress<int>(async percent =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MainProgressBar.Value = percent;
                        SectionStatusTextBlock.Text = $"Step 3: Transferring... {percent}%";
                    });
                });

                await _deploymentService.TransferFileAsync(zipPath, destinationFolder, transferProgress, ct);
                await SetCheckboxAsync(Chk3_TransferZip, true);

                await Dispatcher.InvokeAsync(() => MainProgressBar.IsIndeterminate = true);
                UpdateSectionStatus("Step 3: Validating transferred zip...");

                bool zipExists = await Task.Run(() =>
                    _deploymentService.ValidateRemoteZipExists(destinationFolder, zipFileName), ct);
                if (!zipExists)
                    throw new InvalidOperationException("Transferred zip file not found on remote server.");
                await SetCheckboxAsync(Chk3_ValidateTransfer, true);

                UpdateSectionStatus("Step 3: Unzipping on remote server...");
                await Dispatcher.InvokeAsync(() =>
                {
                    UnzipProgressBar.Visibility = Visibility.Visible;
                    UnzipProgressBar.Value = 0;
                    UnzipProgressLabel.Visibility = Visibility.Visible;
                    UnzipProgressLabel.Text = "Preparing to unzip...";
                    MainProgressBar.IsIndeterminate = false;
                });

                string remoteZipPath = Path.Combine(destinationFolder, zipFileName);
                string remoteExtractFolder = Path.Combine(destinationFolder, folderName);

                var unzipProgress = new Progress<(int Percent, string FileName)>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UnzipProgressBar.Value = p.Percent;
                        UnzipProgressLabel.Text = $"Unzipping {p.Percent}% — {p.FileName}";
                        SectionStatusTextBlock.Text = $"Step 3: Unzipping... {p.Percent}%";
                    });
                });

                await _deploymentService.ExtractZipAsync(remoteZipPath, remoteExtractFolder, unzipProgress);

                await Dispatcher.InvokeAsync(() =>
                {
                    UnzipProgressBar.Visibility = Visibility.Collapsed;
                    UnzipProgressLabel.Text = "✅ Unzip complete";
                    MainProgressBar.IsIndeterminate = true;
                });
                await SetCheckboxAsync(Chk3_UnzipRemote, true);

                bool unzipValid = await Task.Run(() => Directory.Exists(remoteExtractFolder), ct);
                if (!unzipValid)
                    throw new InvalidOperationException("Unzipped folder not found on remote server.");
                await SetCheckboxAsync(Chk3_ValidateUnzip, true);

                // STEP 4
                UpdateSectionStatus("Step 4: Stopping IIS website...");
                await _iisService.StopWebsiteAsync(_serverName, iisWebsiteName);
                await SetCheckboxAsync(Chk4_StopIIS, true);

                UpdateSectionStatus("Step 4: Stopping IIS app pool...");
                string appPoolName = await _iisService.GetWebsiteAppPoolAsync(_serverName, iisWebsiteName);
                await _iisService.StopAppPoolAsync(_serverName, appPoolName);
                await SetCheckboxAsync(Chk4_StopAppPool, true);

                UpdateSectionStatus("Step 4: Backing up config file...");
                string websitePhysicalPath = await _iisService.GetWebsitePhysicalPathAsync(_serverName, iisWebsiteName);
                if (!string.IsNullOrWhiteSpace(_envFileName))
                {
                    string envFilePath = Path.Combine(websitePhysicalPath, _envFileName);
                    await Task.Run(() => _deploymentService.BackupFile(envFilePath, folderName), ct);
                }
                await SetCheckboxAsync(Chk4_BackupEnv, true);

                UpdateSectionStatus("Step 4: Overwriting website contents...");
                await _deploymentService.OverwriteWebsiteContentsAsync(remoteExtractFolder, websitePhysicalPath);
                await SetCheckboxAsync(Chk4_Overwrite, true);

                UpdateSectionStatus("Step 4: Starting IIS website...");
                await _iisService.StartWebsiteAsync(_serverName, iisWebsiteName);
                await SetCheckboxAsync(Chk4_StartIIS, true);

                UpdateSectionStatus("Step 4: Starting IIS app pool...");
                await _iisService.StartAppPoolAsync(_serverName, appPoolName);
                await SetCheckboxAsync(Chk4_StartAppPool, true);

                _deploymentStopwatch?.Stop();
                var elapsed = _deploymentStopwatch?.Elapsed ?? TimeSpan.Zero;
                UpdateSectionStatus($"✅ {_sectionKey} deployment completed! Time: {FormatTimeSpan(elapsed)}");

                MessageBox.Show(
                    $"{_sectionKey} deployment completed successfully!\n\nDeployed: {zipFileName}\nTime Elapsed: {FormatTimeSpan(elapsed)}",
                    "Deployment Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                _deploymentStopwatch?.Stop();
                UpdateSectionStatus($"{_sectionKey} deployment cancelled.");
            }
            catch (Exception ex)
            {
                _deploymentStopwatch?.Stop();
                UpdateSectionStatus($"❌ {_sectionKey} deployment failed: {ex.Message}");
                MessageBox.Show($"Deployment failed:\n{ex.Message}",
                    "Deployment Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExecuteButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                MainProgressBar.Visibility = Visibility.Collapsed;
                MainProgressBar.Value = 0;
                ZipProgressBar.Visibility = Visibility.Collapsed;
                UnzipProgressBar.Visibility = Visibility.Collapsed;
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                await timerUpdateTask;

                string site = GetIISWebsiteName();
                if (!string.IsNullOrWhiteSpace(site))
                    await ApplyWebsiteStatusToUiAsync(site);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource is { IsCancellationRequested: false })
            {
                _cancellationTokenSource.Cancel();
                CancelButton.IsEnabled = false;
                UpdateSectionStatus($"Cancelling {_sectionKey} deployment...");
            }
        }

        #endregion

        #region Helpers

        private void ResetChecklists()
        {
            Chk2_CreateDateFolder.IsChecked = false;
            Chk2_CopyItems.IsChecked = false;
            Chk2_ValidateCopy.IsChecked = false;
            Chk2_ZipFolder.IsChecked = false;
            Chk3_TransferZip.IsChecked = false;
            Chk3_ValidateTransfer.IsChecked = false;
            Chk3_UnzipRemote.IsChecked = false;
            Chk3_ValidateUnzip.IsChecked = false;
            Chk4_StopIIS.IsChecked = false;
            Chk4_StopAppPool.IsChecked = false;
            Chk4_BackupEnv.IsChecked = false;
            Chk4_Overwrite.IsChecked = false;
            Chk4_StartIIS.IsChecked = false;
            Chk4_StartAppPool.IsChecked = false;

            ZipProgressLabel.Text = "";
            ZipProgressLabel.Visibility = Visibility.Collapsed;
            ZipProgressBar.Visibility = Visibility.Collapsed;
            UnzipProgressLabel.Text = "";
            UnzipProgressLabel.Visibility = Visibility.Collapsed;
            UnzipProgressBar.Visibility = Visibility.Collapsed;
        }

        private void UpdateSectionStatus(string message)
        {
            Dispatcher.Invoke(() => SectionStatusTextBlock.Text = message);
            StatusMessageChanged?.Invoke(this, message);
        }

        private async Task SetCheckboxAsync(CheckBox checkbox, bool isChecked)
        {
            await Dispatcher.InvokeAsync(() => checkbox.IsChecked = isChecked);
        }

        private async Task UpdateTimerAsync(Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && stopwatch != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (stopwatch.IsRunning)
                            TimeElapsedTextBlock.Text = $"Time Elapsed: {FormatTimeSpan(stopwatch.Elapsed)}";
                    });

                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when deployment finishes
            }
        }

        private static string FormatTimeSpan(TimeSpan ts) =>
            ts.TotalHours >= 1 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");

        #endregion
    }
}
