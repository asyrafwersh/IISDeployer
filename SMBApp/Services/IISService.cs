using System.Diagnostics;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for managing IIS websites on a remote server via command line
    /// </summary>
    public class IISService
    {
        /// <summary>
        /// Stops an IIS website on the remote server
        /// </summary>
        /// <param name="serverName">Remote server name (e.g., DESKTOP-7TSMFM7)</param>
        /// <param name="websiteName">IIS website name (e.g., DKSHFrontend_UAT)</param>
        public async Task StopWebsiteAsync(string serverName, string websiteName)
        {
            string command = $"Invoke-Command -ComputerName {serverName} -ScriptBlock {{ Stop-Website -Name '{websiteName}' }}";
            await RunPowerShellCommandAsync(command);
        }

        /// <summary>
        /// Starts an IIS website on the remote server
        /// </summary>
        /// <param name="serverName">Remote server name</param>
        /// <param name="websiteName">IIS website name</param>
        public async Task StartWebsiteAsync(string serverName, string websiteName)
        {
            string command = $"Invoke-Command -ComputerName {serverName} -ScriptBlock {{ Start-Website -Name '{websiteName}' }}";
            await RunPowerShellCommandAsync(command);
        }

        /// <summary>
        /// Gets the physical path of an IIS website on the remote server
        /// </summary>
        /// <param name="serverName">Remote server name</param>
        /// <param name="websiteName">IIS website name</param>
        /// <returns>Physical path of the website</returns>
        public async Task<string> GetWebsitePhysicalPathAsync(string serverName, string websiteName)
        {
            string command = $"Invoke-Command -ComputerName {serverName} -ScriptBlock {{ (Get-Website -Name '{websiteName}').PhysicalPath }}";
            string result = await RunPowerShellCommandAsync(command);
            return result.Trim();
        }

        /// <summary>
        /// Runs a PowerShell command and returns the output
        /// </summary>
        private async Task<string> RunPowerShellCommandAsync(string command)
        {
            return await Task.Run(() =>
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo)
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
    }
}