using System.IO;
using System.IO.Compression;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for handling deployment operations (copy, zip, transfer, overwrite)
    /// </summary>
    public class DeploymentService
    {
        /// <summary>
        /// Gets the output folder path within the application directory
        /// </summary>
        public string OutputFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");

        /// <summary>
        /// Gets the backup settings folder path within the application directory
        /// </summary>
        public string BackupSettingsFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup settings");

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
        /// Zips a folder into a .zip file with the same name
        /// </summary>
        /// <param name="folderPath">Folder to zip</param>
        /// <returns>Path to the created zip file</returns>
        public string ZipFolder(string folderPath)
        {
            string zipPath = folderPath + ".zip";

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Optimal, false);
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
        /// Extracts a zip file on the remote path
        /// </summary>
        /// <param name="zipFilePath">Full path to the zip file</param>
        /// <param name="extractToFolder">Folder to extract into</param>
        public void ExtractZip(string zipFilePath, string extractToFolder)
        {
            if (Directory.Exists(extractToFolder))
                Directory.Delete(extractToFolder, true);

            ZipFile.ExtractToDirectory(zipFilePath, extractToFolder);
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
        public void OverwriteWebsiteContents(string sourceFolder, string websiteFolder)
        {
            // Copy all files and directories from source to website folder
            foreach (var file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceFolder, file);
                string destFile = Path.Combine(websiteFolder, relativePath);
                string? destDir = Path.GetDirectoryName(destFile);

                if (destDir != null)
                    Directory.CreateDirectory(destDir);

                File.Copy(file, destFile, true);
            }
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