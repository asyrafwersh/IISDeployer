using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using IISDeployer.Models;
using IISDeployer.Services;

namespace IISDeployer.View
{
    /// <summary>
    /// Interaction logic for SMBPage.xaml
    /// </summary>
    public partial class SMBPage : Page
    {
        private readonly SMBService _smbService;
        private readonly ConfigurationService _configurationService;
        private string? _selectedFilePath;
        private bool _isPasswordVisible = false;
        private bool _isConnected = false;
        private bool _isTransferring = false;
        private CancellationTokenSource? _transferCancellationTokenSource;

        public SMBPage()
        {
            InitializeComponent();
            _smbService = new SMBService();
            _configurationService = new ConfigurationService();
            Loaded += SMBPage_Loaded;
        }

        private async void SMBPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNetworkConfigurations();
            // Hide content initially
            ContentTextBlock.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Loads network configurations from appsettings.json into the ComboBox
        /// </summary>
        private void LoadNetworkConfigurations()
        {
            try
            {
                var configurations = _configurationService.GetNetworkConfigurations();
                NetworkConfigComboBox.ItemsSource = configurations;
                
                if (configurations.Count > 0)
                {
                    NetworkConfigComboBox.SelectedIndex = 0;
                }

                StatusTextBlock.Text = $"Loaded {configurations.Count} saved configuration(s)";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to load configurations: {ex.Message}";
                MessageBox.Show($"Failed to load network configurations:\n{ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Handles selection change in the network configuration dropdown
        /// </summary>
        private void NetworkConfigComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NetworkConfigComboBox.SelectedItem is NetworkConfiguration selectedConfig)
            {
                NetworkPathTextBox.Text = selectedConfig.NetworkPath;
                UsernameTextBox.Text = selectedConfig.Username;
                
                if (_isPasswordVisible)
                {
                    PasswordTextBox.Text = selectedConfig.Password;
                }
                else
                {
                    PasswordBox.Password = selectedConfig.Password;
                }

                StatusTextBlock.Text = $"Loaded configuration: {selectedConfig.Name}";
            }
        }

        /// <summary>
        /// Updates the Transfer File button state based on connection and file selection
        /// </summary>
        private void UpdateTransferButtonState()
        {
            TransferButton.IsEnabled = _isConnected && !string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath) && !_isTransferring;
        }

        /// <summary>
        /// Updates the Browse File button state based on transfer status
        /// </summary>
        private void UpdateBrowseButtonState()
        {
            BrowseFileButton.IsEnabled = !_isTransferring;
        }

        /// <summary>
        /// Connects to the SMB network share
        /// </summary>
        private async Task ConnectToSMBShareAsync()
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
                    MessageBox.Show("Please fill in all connection fields (Network Path, Username, Password)", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() =>
                {
                    bool isConnected = _smbService.ConnectToShare(networkPath, username, password);

                    if (isConnected)
                    {
                        // Test the connection
                        bool isAccessible = _smbService.TestConnection(networkPath);

                        if (!isAccessible)
                        {
                            throw new InvalidOperationException("Connected but path is not accessible");
                        }
                    }
                });

                _isConnected = true;
                StatusTextBlock.Text = $"Successfully connected to {networkPath}";
                await LoadNetworkContentsAsync();
                
                // Show the content when connected
                ContentTextBlock.Visibility = Visibility.Visible;
                
                // Update transfer button state
                UpdateTransferButtonState();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                StatusTextBlock.Text = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect to SMB share:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Hide content on connection failure
                ContentTextBlock.Visibility = Visibility.Collapsed;
                ContentTextBlock.Text = "";
                
                // Update transfer button state
                UpdateTransferButtonState();
            }
        }

        /// <summary>
        /// Loads the contents of the network share
        /// </summary>
        private async Task LoadNetworkContentsAsync()
        {
            try
            {
                string networkPath = NetworkPathTextBox.Text;

                await Task.Run(() =>
                {
                    var files = _smbService.GetFiles(networkPath);
                    var directories = _smbService.GetDirectories(networkPath);

                    Dispatcher.Invoke(() =>
                    {
                        ContentTextBlock.Text = $"Directories: {directories.Length}\nFiles: {files.Length}";
                        ContentTextBlock.Visibility = Visibility.Visible;
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load network contents:\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ContentTextBlock.Visibility = Visibility.Collapsed;
                ContentTextBlock.Text = "";
            }
        }

        /// <summary>
        /// Opens file picker dialog to select a file
        /// </summary>
        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select File to Transfer",
                Filter = "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                SelectedFileTextBlock.Text = _selectedFilePath;
                
                // Update transfer button state based on connection status
                UpdateTransferButtonState();
            }
        }

        /// <summary>
        /// Transfers the selected file to the SMB share with progress tracking
        /// </summary>
        private async void TransferFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Please select a valid file to transfer.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isConnected)
            {
                MessageBox.Show("Please connect to the SMB share first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a new cancellation token source for this transfer
            _transferCancellationTokenSource = new CancellationTokenSource();

            string networkPath = NetworkPathTextBox.Text;
            string fileName = Path.GetFileName(_selectedFilePath);
            string destinationPath = Path.Combine(networkPath, fileName);

            try
            {
                _isTransferring = true;
                TransferButton.IsEnabled = false;
                CancelTransferButton.IsEnabled = true;
                UpdateBrowseButtonState();
                TransferProgressBar.Visibility = Visibility.Visible;
                TransferProgressBar.IsIndeterminate = false;
                TransferProgressBar.Value = 0;

                var progress = new Progress<int>(percent =>
                {
                    TransferProgressBar.Value = percent;
                    StatusTextBlock.Text = $"Transferring file... {percent}%";
                });

                bool transferCompleted = await CopyFileWithProgressAsync(_selectedFilePath, destinationPath, progress, _transferCancellationTokenSource.Token);

                if (transferCompleted)
                {
                    StatusTextBlock.Text = $"File transferred successfully: {fileName}";
                    MessageBox.Show($"File '{fileName}' transferred successfully to {networkPath}", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh the network contents
                    await LoadNetworkContentsAsync();
                }
                else
                {
                    // Transfer was cancelled
                    StatusTextBlock.Text = "File transfer cancelled by user";
                    
                    // Clean up the partial file
                    try
                    {
                        if (File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                            StatusTextBlock.Text = "File transfer cancelled - partial file deleted";
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        StatusTextBlock.Text = $"Transfer cancelled but failed to delete partial file: {deleteEx.Message}";
                    }

                    MessageBox.Show("File transfer was cancelled.", "Transfer Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Transfer failed: {ex.Message}";
                MessageBox.Show($"Failed to transfer file:\n{ex.Message}", "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isTransferring = false;
                TransferProgressBar.Value = 0;
                TransferProgressBar.Visibility = Visibility.Collapsed;
                UpdateTransferButtonState();
                UpdateBrowseButtonState();
                CancelTransferButton.IsEnabled = false;
                
                // Dispose of the cancellation token source
                _transferCancellationTokenSource?.Dispose();
                _transferCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cancels the current file transfer operation
        /// </summary>
        private void CancelTransferButton_Click(object sender, RoutedEventArgs e)
        {
            if (_transferCancellationTokenSource != null && !_transferCancellationTokenSource.IsCancellationRequested)
            {
                _transferCancellationTokenSource.Cancel();
                StatusTextBlock.Text = "Cancelling transfer...";
                CancelTransferButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Copies a file with progress reporting and cancellation support
        /// </summary>
        /// <param name="sourceFile">Source file path</param>
        /// <param name="destinationFile">Destination file path</param>
        /// <param name="progress">Progress reporter for percentage updates</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <param name="bufferSize">Buffer size for copying (default 1MB)</param>
        /// <returns>True if transfer completed successfully, False if cancelled</returns>
        private async Task<bool> CopyFileWithProgressAsync(string sourceFile, string destinationFile, IProgress<int> progress, CancellationToken cancellationToken, int bufferSize = 1024 * 1024)
        {
            return await Task.Run(() =>
            {
                var fileInfo = new FileInfo(sourceFile);
                long totalBytes = fileInfo.Length;
                long totalBytesRead = 0;
                int lastReportedProgress = 0;

                using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
                using (var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Check for cancellation before writing
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return false; // Transfer cancelled
                        }

                        destinationStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        // Calculate percentage
                        int percentComplete = (int)((double)totalBytesRead / totalBytes * 100);

                        // Only report progress if it changed (avoid excessive UI updates)
                        if (percentComplete != lastReportedProgress)
                        {
                            progress.Report(percentComplete);
                            lastReportedProgress = percentComplete;
                        }
                    }

                    // Ensure all data is written to disk
                    destinationStream.Flush();
                }

                return true; // Transfer completed successfully
            }, cancellationToken);
        }

        /// <summary>
        /// Toggles password visibility between hidden and visible
        /// </summary>
        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                // Hide password
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                EyeIcon.Text = "👁";
                _isPasswordVisible = false;
            }
            else
            {
                // Show password
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                EyeIcon.Text = "🙈";
                _isPasswordVisible = true;
            }
        }

        /// <summary>
        /// Manually trigger connection (can be called from a button)
        /// </summary>
        public async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToSMBShareAsync();
        }

        /// <summary>
        /// Disconnect from the SMB share
        /// </summary>
        public void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Cancel any ongoing transfer before disconnecting
                if (_isTransferring && _transferCancellationTokenSource != null && !_transferCancellationTokenSource.IsCancellationRequested)
                {
                    _transferCancellationTokenSource.Cancel();
                    StatusTextBlock.Text = "Cancelling transfer before disconnect...";
                    
                    // Give a brief moment for the transfer to cancel
                    Task.Delay(500).Wait();
                }

                string networkPath = NetworkPathTextBox.Text;
                bool disconnected = _smbService.DisconnectFromShare(networkPath);
                
                if (disconnected)
                {
                    _isConnected = false;
                    StatusTextBlock.Text = "Disconnected successfully";
                    
                    // Hide the directories/files information when disconnected
                    ContentTextBlock.Visibility = Visibility.Collapsed;
                    ContentTextBlock.Text = "";
                    
                    // Disable transfer button when disconnected
                    UpdateTransferButtonState();
                }
                else
                {
                    StatusTextBlock.Text = "Disconnect failed";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to disconnect:\n{ex.Message}", "Disconnect Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}