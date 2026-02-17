using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SMBApp.Models;
using SMBApp.Services;

namespace SMBApp.View
{
    /// <summary>
    /// Represents a selected file or folder item for display
    /// </summary>
    public class SelectedItemDisplay
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";
    }

    /// <summary>
    /// Interaction logic for ReturnsDeploymentPage.xaml
    /// </summary>
    public partial class ReturnsDeploymentPage : Page
    {
        private readonly SMBService _smbService;
        private readonly ConfigurationService _configurationService;
        private readonly DeploymentService _deploymentService;
        private readonly IISService _iisService;

        private bool _isPasswordVisible = false;
        private bool _isConnected = false;
        private string _parentSourceFolder = string.Empty;
        private readonly List<string> _selectedPaths = new();
        private CancellationTokenSource? _executionCancellationTokenSource;

        public ReturnsDeploymentPage()
        {
            InitializeComponent();
            _smbService = new SMBService();
            _configurationService = new ConfigurationService();
            _deploymentService = new DeploymentService();
            _iisService = new IISService();

            Loaded += ReturnsDeploymentPage_Loaded;
        }

        private void ReturnsDeploymentPage_Loaded(object sender, RoutedEventArgs e)
        {
            _deploymentService.EnsureFoldersExist();
            LoadNetworkConfigurations();
            LoadSavedDeploymentSettings();
        }

        #region Header / Connection

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

                StatusTextBlock.Text = $"Loaded configuration: {selectedConfig.Name}";
            }
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
                string password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

                if (string.IsNullOrWhiteSpace(networkPath) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
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

        #region Step 1: Select Files and Folders

        private void LoadSavedDeploymentSettings()
        {
            try
            {
                var settings = _configurationService.GetDeploymentSettings();
                var frontend = settings.Frontend;

                if (!string.IsNullOrWhiteSpace(frontend.ParentSourceFolder))
                {
                    _parentSourceFolder = frontend.ParentSourceFolder;
                    ParentSourceFolderLabel.Text = _parentSourceFolder;
                    ParentSourceFolderLabel.Foreground = System.Windows.Media.Brushes.Black;
                }

                if (!string.IsNullOrWhiteSpace(frontend.DestinationSubFolder))
                {
                    DestinationSubFolderTextBox.Text = frontend.DestinationSubFolder;
                }

                if (frontend.SelectedPaths.Count > 0)
                {
                    _selectedPaths.Clear();
                    _selectedPaths.AddRange(frontend.SelectedPaths);
                    RefreshSelectedItemsDisplay();
                }
            }
            catch
            {
                // Settings may not exist yet, ignore
            }
        }

        private void SelectParentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Parent Source Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                _parentSourceFolder = dialog.FolderName;
                ParentSourceFolderLabel.Text = _parentSourceFolder;
                ParentSourceFolderLabel.Foreground = System.Windows.Media.Brushes.Black;
                StatusTextBlock.Text = $"Parent folder set: {_parentSourceFolder}";
            }
        }

        private void SelectFilesAndFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_parentSourceFolder) || !Directory.Exists(_parentSourceFolder))
            {
                MessageBox.Show("Please select a valid parent source folder first.",
                    "No Parent Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Use OpenFileDialog to select files (user can also type folder names)
            var dialog = new OpenFileDialog
            {
                Title = "Select Files to Deploy",
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
            }

            // Also allow folder selection
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Folder to Deploy",
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
            }

            // Add pre-set folders if they exist and are not already added
            AddPresetFolders();

            RefreshSelectedItemsDisplay();
            StatusTextBlock.Text = $"{_selectedPaths.Count} item(s) selected";
        }

        /// <summary>
        /// Adds pre-set ".next" and "public" folders if they exist in the parent source folder
        /// </summary>
        private void AddPresetFolders()
        {
            string[] presetFolders = [".next", "public"];

            foreach (var folder in presetFolders)
            {
                string fullPath = Path.Combine(_parentSourceFolder, folder);
                if (Directory.Exists(fullPath) && !_selectedPaths.Contains(fullPath))
                {
                    _selectedPaths.Add(fullPath);
                }
            }
        }

        private void RemoveSelectedItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string path)
            {
                _selectedPaths.Remove(path);
                RefreshSelectedItemsDisplay();
                StatusTextBlock.Text = $"{_selectedPaths.Count} item(s) selected";
            }
        }

        private void RefreshSelectedItemsDisplay()
        {
            var displayItems = _selectedPaths.Select(p => new SelectedItemDisplay
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
            try
            {
                var settings = _configurationService.GetDeploymentSettings();
                settings.Frontend.ParentSourceFolder = _parentSourceFolder;
                settings.Frontend.SelectedPaths = new List<string>(_selectedPaths);
                settings.Frontend.DestinationSubFolder = DestinationSubFolderTextBox.Text;

                _configurationService.SaveDeploymentSettings(settings);

                StatusTextBlock.Text = "Settings saved successfully";
                MessageBox.Show("Deployment settings saved to appsettings.json.",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings:\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Execute Deployment (Steps 2-4)

        private async void ExecuteFrontendButton_Click(object sender, RoutedEventArgs e)
        {
            // --- Validation ---
            if (!_isConnected)
            {
                MessageBox.Show("Please connect to the SMB share first.",
                    "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string networkPath = NetworkPathTextBox.Text;
            string destSubFolder = DestinationSubFolderTextBox.Text.Trim();
            string destinationFolder = string.IsNullOrWhiteSpace(destSubFolder)
                ? networkPath
                : Path.Combine(networkPath, destSubFolder);

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

            // Validate selected paths exist
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

            // --- Reset checklists ---
            ResetAllChecklists();

            _executionCancellationTokenSource = new CancellationTokenSource();
            ExecuteFrontendButton.IsEnabled = false;
            FrontendProgressBar.Visibility = Visibility.Visible;
            FrontendProgressBar.IsIndeterminate = true;

            string folderName = _deploymentService.GenerateFrontendFolderName();
            string zipFileName = folderName + ".zip";

            try
            {
                var ct = _executionCancellationTokenSource.Token;
                var settings = _configurationService.GetDeploymentSettings();
                string iisWebsiteName = settings.Frontend.IISWebsiteName;
                // Extract server name from network path (e.g., \\SERVER\Share -> SERVER)
                string serverName = ExtractServerName(networkPath);

                // ===== STEP 2: Zip Files =====
                StatusTextBlock.Text = "Step 2: Creating date subfolder...";
                string dateFolderPath = _deploymentService.GetOrCreateDateSubFolder();
                Chk2_CreateDateFolder.IsChecked = true;

                StatusTextBlock.Text = "Step 2: Copying selected items...";
                string stagingFolder = Path.Combine(dateFolderPath, folderName);
                await _deploymentService.CopySelectedItemsAsync(_parentSourceFolder, _selectedPaths, stagingFolder, ct);
                Chk2_CopyItems.IsChecked = true;

                StatusTextBlock.Text = "Step 2: Validating copied items...";
                bool valid = _deploymentService.ValidateCopiedItems(stagingFolder, _selectedPaths, _parentSourceFolder);
                if (!valid)
                    throw new InvalidOperationException("Copied items validation failed. Some items are missing.");
                Chk2_ValidateCopy.IsChecked = true;

                StatusTextBlock.Text = "Step 2: Zipping folder...";
                string zipPath = _deploymentService.ZipFolder(stagingFolder);
                Chk2_ZipFolder.IsChecked = true;

                // ===== STEP 3: Transfer =====
                StatusTextBlock.Text = "Step 3: Transferring zip to remote server...";
                FrontendProgressBar.IsIndeterminate = false;
                FrontendProgressBar.Value = 0;

                var transferProgress = new Progress<int>(percent =>
                {
                    FrontendProgressBar.Value = percent;
                    StatusTextBlock.Text = $"Step 3: Transferring... {percent}%";
                });

                await _deploymentService.TransferFileAsync(zipPath, destinationFolder, transferProgress, ct);
                Chk3_TransferZip.IsChecked = true;

                FrontendProgressBar.IsIndeterminate = true;
                StatusTextBlock.Text = "Step 3: Validating transferred zip...";
                bool zipExists = _deploymentService.ValidateRemoteZipExists(destinationFolder, zipFileName);
                if (!zipExists)
                    throw new InvalidOperationException("Transferred zip file not found on remote server.");
                Chk3_ValidateTransfer.IsChecked = true;

                StatusTextBlock.Text = "Step 3: Unzipping on remote server...";
                string remoteZipPath = Path.Combine(destinationFolder, zipFileName);
                string remoteExtractFolder = Path.Combine(destinationFolder, folderName);
                _deploymentService.ExtractZip(remoteZipPath, remoteExtractFolder);
                Chk3_UnzipRemote.IsChecked = true;

                bool unzipValid = Directory.Exists(remoteExtractFolder);
                if (!unzipValid)
                    throw new InvalidOperationException("Unzipped folder not found on remote server.");
                Chk3_ValidateUnzip.IsChecked = true;

                // ===== STEP 4: IIS Stop, Backup, Overwrite, Start =====
                StatusTextBlock.Text = "Step 4: Stopping IIS website...";
                await _iisService.StopWebsiteAsync(serverName, iisWebsiteName);
                Chk4_StopIIS.IsChecked = true;

                StatusTextBlock.Text = "Step 4: Backing up .env file...";
                string websitePhysicalPath = await _iisService.GetWebsitePhysicalPathAsync(serverName, iisWebsiteName);
                // If physical path uses env variables like %SystemDrive%, it may need expanding on remote — 
                // here we assume the path is a UNC-accessible path or mapped drive scenario
                string envFilePath = Path.Combine(websitePhysicalPath, settings.Frontend.EnvFileName);
                _deploymentService.BackupFile(envFilePath, folderName);
                Chk4_BackupEnv.IsChecked = true;

                StatusTextBlock.Text = "Step 4: Overwriting website contents...";
                _deploymentService.OverwriteWebsiteContents(remoteExtractFolder, websitePhysicalPath);
                Chk4_Overwrite.IsChecked = true;

                StatusTextBlock.Text = "Step 4: Starting IIS website...";
                await _iisService.StartWebsiteAsync(serverName, iisWebsiteName);
                Chk4_StartIIS.IsChecked = true;

                StatusTextBlock.Text = "✅ Deployment completed successfully!";
                MessageBox.Show($"Frontend deployment completed successfully!\n\nDeployed: {zipFileName}",
                    "Deployment Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text = "Deployment cancelled.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"❌ Deployment failed: {ex.Message}";
                MessageBox.Show($"Deployment failed:\n{ex.Message}",
                    "Deployment Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExecuteFrontendButton.IsEnabled = true;
                FrontendProgressBar.Visibility = Visibility.Collapsed;
                FrontendProgressBar.Value = 0;
                _executionCancellationTokenSource?.Dispose();
                _executionCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Extracts the server name from a UNC path (e.g., \\SERVER\Share -> SERVER)
        /// </summary>
        private static string ExtractServerName(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath) || !uncPath.StartsWith(@"\\"))
                return uncPath;

            string withoutPrefix = uncPath[2..];
            int slashIndex = withoutPrefix.IndexOf('\\');
            return slashIndex > 0 ? withoutPrefix[..slashIndex] : withoutPrefix;
        }

        /// <summary>
        /// Resets all step checklists to unchecked
        /// </summary>
        private void ResetAllChecklists()
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
            Chk4_BackupEnv.IsChecked = false;
            Chk4_Overwrite.IsChecked = false;
            Chk4_StartIIS.IsChecked = false;
        }

        #endregion
    }
}