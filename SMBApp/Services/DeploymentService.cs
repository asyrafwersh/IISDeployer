using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for handling deployment operations (copy, zip, transfer, overwrite)
    /// </summary>
    public class DeploymentService
    {
        private string? _username;
        private string? _password;

        /// <summary>
        /// Gets the output folder path within the application directory
        /// </summary>
        public string OutputFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");

        /// <summary>
        /// Gets the backup settings folder path within the application directory
        /// </summary>
        public string BackupSettingsFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup settings");

        /// <summary>
        /// Sets the credentials to use for remote PowerShell operations
        /// </summary>
        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        /// <summary>
        /// Ensures the Output and Backup settings folders exist
        /// </summary>
        public void EnsureFoldersExist()
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(BackupSettingsFolder);
        }

        /// <summary>
        /// Generates a deployment folder name based on current date/time
        /// e.g., Frontend1430_17022026
        /// </summary>
        public string GenerateFrontendFolderName()
        {
            var now = DateTime.Now;
            return $"Frontend{now:HHmm}_{now:ddMMyyyy}";
        }

        /// <summary>
        /// Generates a deployment folder name based on current date/time
        /// e.g., API1430_17022026
        /// </summary>
        public string GenerateAPIFolderName()
        {
            var now = DateTime.Now;
            return $"API{now:HHmm}_{now:ddMMyyyy}";
        }

        /// <summary>
        /// Generates a deployment folder name for any section key
        /// e.g., Scheduler1430_17022026
        /// </summary>
        public string GenerateSectionFolderName(string sectionKey)
        {
            var now = DateTime.Now;
            return $"{sectionKey}{now:HHmm}_{now:ddMMyyyy}";
        }

        /// <summary>
        /// Gets or creates a date-based subfolder (DDMMYYYY) inside the Output folder
        /// </summary>
        public string GetOrCreateDateSubFolder()
        {
            string dateFolderName = DateTime.Now.ToString("ddMMyyyy");
            string dateFolderPath = Path.Combine(OutputFolder, dateFolderName);
            Directory.CreateDirectory(dateFolderPath);
            return dateFolderPath;
        }

        /// <summary>
        /// Copies selected files/folders from parent source folder into a staging folder
        /// </summary>
        /// <param name="parentSourceFolder">The parent source folder</param>
        /// <param name="selectedPaths">List of selected relative or absolute paths</param>
        /// <param name="destinationFolder">The staging folder to copy into</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task CopySelectedItemsAsync(string parentSourceFolder, List<string> selectedPaths, string destinationFolder, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destinationFolder);

            foreach (var path in selectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fullSourcePath = Path.IsPathRooted(path) ? path : Path.Combine(parentSourceFolder, path);
                string itemName = Path.GetFileName(fullSourcePath);
                string destPath = Path.Combine(destinationFolder, itemName);

                if (Directory.Exists(fullSourcePath))
                {
                    await Task.Run(() => CopyDirectoryRecursive(fullSourcePath, destPath, cancellationToken), cancellationToken);
                }
                else if (File.Exists(fullSourcePath))
                {
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                    File.Copy(fullSourcePath, destPath, true);
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {fullSourcePath}");
                }
            }
        }

        /// <summary>
        /// Validates that all expected items exist in the staging folder
        /// </summary>
        public bool ValidateCopiedItems(string stagingFolder, List<string> selectedPaths, string parentSourceFolder)
        {
            foreach (var path in selectedPaths)
            {
                string fullSourcePath = Path.IsPathRooted(path) ? path : Path.Combine(parentSourceFolder, path);
                string itemName = Path.GetFileName(fullSourcePath);
                string destPath = Path.Combine(stagingFolder, itemName);

                if (!Directory.Exists(destPath) && !File.Exists(destPath))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Zips a folder into a .zip file with the same name, reporting progress as a percentage.
        /// </summary>
        /// <param name="folderPath">Folder to zip</param>
        /// <param name="progress">Progress reporter (0-100 percentage)</param>
        public string ZipFolder(string folderPath, IProgress<(int Percent, string FileName)>? progress = null)
        {
            string zipPath = folderPath + ".zip";

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            if (progress == null)
            {
                ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Optimal, false);
                return zipPath;
            }

            // Enumerate all files for progress tracking
            var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            int totalFiles = allFiles.Length;
            int processedFiles = 0;

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in allFiles)
            {
                string entryName = Path.GetRelativePath(folderPath, file);
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);

                processedFiles++;
                int percent = totalFiles > 0 ? (int)((double)processedFiles / totalFiles * 100) : 100;
                progress.Report((percent, Path.GetFileName(file)));
            }

            return zipPath;
        }

        /// <summary>
        /// Transfers a file to a remote destination via file copy (SMB connected path)
        /// </summary>
        public async Task TransferFileAsync(string sourceFile, string destinationFolder, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            string fileName = Path.GetFileName(sourceFile);
            string destPath = Path.Combine(destinationFolder, fileName);

            var fileInfo = new FileInfo(sourceFile);
            long totalBytes = fileInfo.Length;
            long totalBytesRead = 0;
            int bufferSize = 1024 * 1024; // 1MB

            await Task.Run(() =>
            {
                using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
                using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan);

                byte[] buffer = new byte[bufferSize];
                int bytesRead;
                int lastReported = 0;

                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    destStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    int percent = (int)((double)totalBytesRead / totalBytes * 100);
                    if (percent != lastReported)
                    {
                        progress?.Report(percent);
                        lastReported = percent;
                    }
                }

                destStream.Flush();
            }, cancellationToken);
        }

        /// <summary>
        /// Validates that a zip file exists at the given remote path
        /// </summary>
        public bool ValidateRemoteZipExists(string remoteFolderPath, string zipFileName)
        {
            string fullPath = Path.Combine(remoteFolderPath, zipFileName);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Extracts a zip file on the remote path using PowerShell with WinRM over HTTP,
        /// streaming per-file progress back via the <paramref name="progress"/> callback.
        /// </summary>
        /// <param name="zipFilePath">Full UNC path to the zip file</param>
        /// <param name="extractToFolder">Full UNC path to extract into</param>
        /// <param name="progress">Reports (Percent, FileName) for each extracted file</param>
        public async Task ExtractZipAsync(string zipFilePath, string extractToFolder, IProgress<(int Percent, string FileName)>? progress = null)
        {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            {
                throw new InvalidOperationException("Credentials not set. Call SetCredentials() first.");
            }

            string serverName = ExtractServerNameFromPath(zipFilePath);

            // PowerShell script that extracts file-by-file and writes progress lines
            // in the format "PROGRESS:<percent>|<filename>" so we can parse them.
            // Uses single-quoted concatenation to avoid escaping issues when the command
            // passes through C# string interpolation and the -Command argument.
            string psCommand = $@"
                if (Test-Path '{extractToFolder}') {{
                    Remove-Item -Path '{extractToFolder}' -Recurse -Force
                }}
                New-Item -Path '{extractToFolder}' -ItemType Directory -Force | Out-Null

                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $zip = [System.IO.Compression.ZipFile]::OpenRead('{zipFilePath}')
                try {{
                    $totalEntries = $zip.Entries.Count
                    $current = 0
                    foreach ($entry in $zip.Entries) {{
                        $current++
                        $destPath = Join-Path '{extractToFolder}' $entry.FullName
                        $destDir = Split-Path $destPath -Parent
                        if (-not (Test-Path $destDir)) {{
                            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
                        }}
                        if ($entry.FullName -notmatch '/$') {{
                            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
                        }}
                        $pct = [math]::Floor(($current / $totalEntries) * 100)
                        Write-Output ('PROGRESS:' + $pct + '|' + $entry.FullName)
                    }}
                }} finally {{
                    $zip.Dispose()
                }}
            ";

            if (progress == null)
            {
                await RunRemotePowerShellCommandAsync(serverName, psCommand);
                return;
            }

            await RunRemotePowerShellWithProgressAsync(serverName, psCommand, progress);
        }

        /// <summary>
        /// Runs a remote PowerShell command and streams stdout line-by-line,
        /// parsing lines matching "PROGRESS:&lt;percent&gt;|&lt;filename&gt;" into the progress callback.
        /// </summary>
        private async Task RunRemotePowerShellWithProgressAsync(
            string serverName,
            string command,
            IProgress<(int Percent, string FileName)> progress)
        {
            await Task.Run(() =>
            {
                string escapedPassword = _password!.Replace("'", "''").Replace("$", "`$").Replace("`", "``");

                string fullScript = $@"
                    $securePassword = ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force
                    $credential = New-Object System.Management.Automation.PSCredential('{_username}', $securePassword)

                    $sessionOption = New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck

                    Invoke-Command -ComputerName {serverName} -Credential $credential -SessionOption $sessionOption -ScriptBlock {{
                        {command}
                    }}
                ";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{fullScript.Replace("\"", "`\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo)
                    ?? throw new InvalidOperationException("Failed to start PowerShell process");

                // Read stdout line-by-line for real-time progress
                string? line;
                string lastError = string.Empty;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    if (line.StartsWith("PROGRESS:"))
                    {
                        // Parse "PROGRESS:<percent>|<filename>"
                        string payload = line["PROGRESS:".Length..];
                        int pipeIndex = payload.IndexOf('|');
                        if (pipeIndex > 0 &&
                            int.TryParse(payload[..pipeIndex], out int percent))
                        {
                            string fileName = payload[(pipeIndex + 1)..];
                            progress.Report((percent, fileName));
                        }
                    }
                }

                lastError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(lastError))
                    throw new InvalidOperationException($"PowerShell command failed: {lastError}");
            });
        }

        /// <summary>
        /// Runs a PowerShell command on a remote server using WinRM over HTTP with credentials
        /// </summary>
        private async Task<string> RunRemotePowerShellCommandAsync(string serverName, string command)
        {
            return await Task.Run(() =>
            {
                // Escape special characters in password for PowerShell
                string escapedPassword = _password!.Replace("'", "''").Replace("$", "`$").Replace("`", "``");
                
                // Build the PowerShell script with credential creation and remote execution
                string fullScript = $@"
                    $securePassword = ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force
                    $credential = New-Object System.Management.Automation.PSCredential('{_username}', $securePassword)
                    
                    $sessionOption = New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck
                    
                    Invoke-Command -ComputerName {serverName} -Credential $credential -SessionOption $sessionOption -ScriptBlock {{
                        {command}
                    }}
                ";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{fullScript.Replace("\"", "`\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo)
                    ?? throw new InvalidOperationException("Failed to start PowerShell process");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    throw new InvalidOperationException($"PowerShell command failed: {error}");
                }

                return output;
            });
        }

        /// <summary>
        /// Extracts server name from a UNC path (e.g., \\SERVER\Share\file.zip -> SERVER)
        /// </summary>
        private static string ExtractServerNameFromPath(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath) || !uncPath.StartsWith(@"\\"))
                throw new ArgumentException("Path must be a UNC path starting with \\\\", nameof(uncPath));

            string withoutPrefix = uncPath[2..];
            int slashIndex = withoutPrefix.IndexOf('\\');
            return slashIndex > 0 ? withoutPrefix[..slashIndex] : withoutPrefix;
        }

        /// <summary>
        /// Backs up a file from the IIS website folder to the backup settings folder
        /// </summary>
        public void BackupFile(string sourceFilePath, string backupSubFolder)
        {
            string backupFolder = Path.Combine(BackupSettingsFolder, backupSubFolder);
            Directory.CreateDirectory(backupFolder);

            string fileName = Path.GetFileName(sourceFilePath);
            string destPath = Path.Combine(backupFolder, fileName);

            if (File.Exists(sourceFilePath))
            {
                File.Copy(sourceFilePath, destPath, true);
            }
        }

        /// <summary>
        /// Overwrites the contents of the IIS website folder with the unzipped deployment contents
        /// </summary>
        /// <param name="sourceFolder">Source folder (UNC path)</param>
        /// <param name="websiteFolder">Website folder (local path on remote server)</param>
        public async Task OverwriteWebsiteContentsAsync(string sourceFolder, string websiteFolder)
        {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            {
                throw new InvalidOperationException("Credentials not set. Call SetCredentials() first.");
            }

            // Extract server name from source folder UNC path
            string serverName = ExtractServerNameFromPath(sourceFolder);

            // PowerShell command to copy files from source to website folder
            string psCommand = $@"
                $sourceFolder = '{sourceFolder}'
                $websiteFolder = '{websiteFolder}'
                
                # Get all files from source folder recursively
                $files = Get-ChildItem -Path $sourceFolder -Recurse -File
                
                foreach ($file in $files) {{
                    # Calculate relative path
                    $relativePath = $file.FullName.Substring($sourceFolder.Length).TrimStart('\')
                    $destFile = Join-Path -Path $websiteFolder -ChildPath $relativePath
                    $destDir = Split-Path -Path $destFile -Parent
                    
                    # Create destination directory if it doesn't exist
                    if (-not (Test-Path -Path $destDir)) {{
                        New-Item -Path $destDir -ItemType Directory -Force | Out-Null
                    }}
                    
                    # Copy file with overwrite
                    Copy-Item -Path $file.FullName -Destination $destFile -Force
                }}
            ";

            await RunRemotePowerShellCommandAsync(serverName, psCommand);
        }

        /// <summary>
        /// Recursively copies a directory
        /// </summary>
        private void CopyDirectoryRecursive(string sourceDir, string destDir, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSubDir, cancellationToken);
            }
        }
    }
}