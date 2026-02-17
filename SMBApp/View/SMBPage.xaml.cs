using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SMBApp.Services;

namespace SMBApp.View
{
    /// <summary>
    /// Interaction logic for SMBPage.xaml
    /// </summary>
    public partial class SMBPage : Page
    {
        private readonly SMBService _smbService;
        private const string NetworkPath = @"\\DESKTOP-3GO5301\MiCCTV";
        private const string Username = "mihome";
        private const string Password = "@Kep120018";
        private string? _selectedFilePath;

        public SMBPage()
        {
            InitializeComponent();
            _smbService = new SMBService();
            Loaded += SMBPage_Loaded;
        }

        private async void SMBPage_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectToSMBShareAsync();
        }

        /// <summary>
        /// Connects to the SMB network share
        /// </summary>
        private async Task ConnectToSMBShareAsync()
        {
            try
            {
                StatusTextBlock.Text = "Connecting to SMB share...";

                await Task.Run(() =>
                {
                    bool isConnected = _smbService.ConnectToShare(NetworkPath, Username, Password);

                    if (isConnected)
                    {
                        // Test the connection
                        bool isAccessible = _smbService.TestConnection(NetworkPath);

                        if (!isAccessible)
                        {
                            throw new InvalidOperationException("Connected but path is not accessible");
                        }
                    }
                });

                StatusTextBlock.Text = $"Successfully connected to {NetworkPath}";
                await LoadNetworkContentsAsync();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect to SMB share:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads the contents of the network share
        /// </summary>
        private async Task LoadNetworkContentsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var files = _smbService.GetFiles(NetworkPath);
                    var directories = _smbService.GetDirectories(NetworkPath);

                    Dispatcher.Invoke(() =>
                    {
                        ContentTextBlock.Text = $"Directories: {directories.Length}\nFiles: {files.Length}";
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load network contents:\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                TransferButton.IsEnabled = true;
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

            try
            {
                TransferButton.IsEnabled = false;
                TransferProgressBar.Visibility = Visibility.Visible;
                TransferProgressBar.IsIndeterminate = false;
                TransferProgressBar.Value = 0;

                string fileName = Path.GetFileName(_selectedFilePath);
                string destinationPath = Path.Combine(NetworkPath, fileName);

                var progress = new Progress<int>(percent =>
                {
                    TransferProgressBar.Value = percent;
                    StatusTextBlock.Text = $"Transferring file... {percent}%";
                });

                await CopyFileWithProgressAsync(_selectedFilePath, destinationPath, progress);

                StatusTextBlock.Text = $"File transferred successfully: {fileName}";
                MessageBox.Show($"File '{fileName}' transferred successfully to {NetworkPath}", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the network contents
                await LoadNetworkContentsAsync();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Transfer failed: {ex.Message}";
                MessageBox.Show($"Failed to transfer file:\n{ex.Message}", "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TransferProgressBar.Value = 0;
                TransferProgressBar.Visibility = Visibility.Collapsed;
                TransferButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Copies a file with progress reporting
        /// </summary>
        /// <param name="sourceFile">Source file path</param>
        /// <param name="destinationFile">Destination file path</param>
        /// <param name="progress">Progress reporter for percentage updates</param>
        /// <param name="bufferSize">Buffer size for copying (default 1MB)</param>
        private async Task CopyFileWithProgressAsync(string sourceFile, string destinationFile, IProgress<int> progress, int bufferSize = 1024 * 1024)
        {
            await Task.Run(() =>
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
                }
            });
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
                bool disconnected = _smbService.DisconnectFromShare(NetworkPath);
                StatusTextBlock.Text = disconnected ? "Disconnected successfully" : "Disconnect failed";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to disconnect:\n{ex.Message}", "Disconnect Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}