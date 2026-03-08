using System.Diagnostics;

namespace SMBApp.Services
{
    /// <summary>
    /// Holds the live status of an IIS website and its application pool
    /// </summary>
    public class IISWebsiteStatus
    {
        public string WebsiteName { get; init; } = string.Empty;
        public string AppPoolName { get; init; } = string.Empty;
        public bool IsWebsiteStarted { get; init; }
        public bool IsAppPoolStarted { get; init; }
    }

    /// <summary>
    /// Service for managing IIS websites on a remote server via command line
    /// </summary>
    public class IISService
    {
        private string? _username;
        private string? _password;

        /// <summary>
        /// Sets the credentials to use for remote PowerShell operations
        /// </summary>
        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        /// <summary>
        /// Stops an IIS website on the remote server. Does nothing if website is already stopped.
        /// </summary>
        public async Task StopWebsiteAsync(string serverName, string websiteName)
        {
            string command = $@"
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                $state = (Get-WebsiteState -Name '{websiteName}' -ErrorAction SilentlyContinue).Value
                if ($state -eq 'Started') {{ Stop-Website -Name '{websiteName}' }}
            ";
            await RunRemotePowerShellCommandAsync(serverName, command);
        }

        /// <summary>
        /// Starts an IIS website on the remote server. Does nothing if website is already started.
        /// </summary>
        public async Task StartWebsiteAsync(string serverName, string websiteName)
        {
            string command = $@"
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                $state = (Get-WebsiteState -Name '{websiteName}' -ErrorAction SilentlyContinue).Value
                if ($state -eq 'Stopped') {{ Start-Website -Name '{websiteName}' }}
            ";
            await RunRemotePowerShellCommandAsync(serverName, command);
        }

        /// <summary>
        /// Stops an IIS application pool on the remote server. Does nothing if app pool is already stopped.
        /// </summary>
        public async Task StopAppPoolAsync(string serverName, string appPoolName)
        {
            string command = $@"
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                $state = (Get-WebAppPoolState -Name '{appPoolName}' -ErrorAction SilentlyContinue).Value
                if ($state -eq 'Started') {{ Stop-WebAppPool -Name '{appPoolName}' }}
            ";
            await RunRemotePowerShellCommandAsync(serverName, command);
        }

        /// <summary>
        /// Starts an IIS application pool on the remote server. Does nothing if app pool is already started.
        /// </summary>
        public async Task StartAppPoolAsync(string serverName, string appPoolName)
        {
            string command = $@"
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                $state = (Get-WebAppPoolState -Name '{appPoolName}' -ErrorAction SilentlyContinue).Value
                if ($state -eq 'Stopped') {{ Start-WebAppPool -Name '{appPoolName}' }}
            ";
            await RunRemotePowerShellCommandAsync(serverName, command);
        }

        /// <summary>
        /// Gets the physical path of an IIS website on the remote server
        /// </summary>
        public async Task<string> GetWebsitePhysicalPathAsync(string serverName, string websiteName)
        {
            string command = $"(Get-Website -Name '{websiteName}').PhysicalPath";
            string result = await RunRemotePowerShellCommandAsync(serverName, command);
            return result.Trim();
        }

        /// <summary>
        /// Gets all IIS website names from the remote server
        /// </summary>
        public async Task<List<string>> GetWebsitesAsync(string serverName)
        {
            string command = "Get-Website | Select-Object -ExpandProperty Name";
            string result = await RunRemotePowerShellCommandAsync(serverName, command);

            return [.. result
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s)];
        }

        /// <summary>
        /// Gets the application pool name associated with a specific IIS website
        /// </summary>
        public async Task<string> GetWebsiteAppPoolAsync(string serverName, string websiteName)
        {
            string command = $"(Get-Website -Name '{websiteName}').ApplicationPool";
            string result = await RunRemotePowerShellCommandAsync(serverName, command);
            return result.Trim();
        }

        /// <summary>
        /// Gets the live status of a website and its application pool in a single remote call.
        /// Returns null if the website is not found.
        /// Emits three separate Write-Output lines to avoid delimiter/type-coercion issues
        /// with IIS State enums and app pool names containing special characters.
        /// </summary>
        public async Task<IISWebsiteStatus?> GetWebsiteStatusAsync(string serverName, string websiteName)
        {
            // Output each value on its own line to avoid any pipe/delimiter ambiguity.
            // [string] cast ensures enum integers are coerced to their named string form.
            string command = $@"
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                $site = Get-Website -Name '{websiteName}'
                if ($site) {{
                    $poolName  = [string]$site.ApplicationPool
                    $siteState = (Get-WebsiteState -Name '{websiteName}').Value
                    $poolState = (Get-WebAppPoolState -Name $poolName).Value
                    Write-Output $siteState
                    Write-Output $poolName
                    Write-Output $poolState
                }}
            ";

            string result = await RunRemotePowerShellCommandAsync(serverName, command);

            string[] lines = result
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();

            // Expect exactly 3 lines: siteState, poolName, poolState
            if (lines.Length < 3)
                return null;

            return new IISWebsiteStatus
            {
                WebsiteName = websiteName,
                AppPoolName = lines[1],
                IsWebsiteStarted = lines[0].Equals("Started", StringComparison.OrdinalIgnoreCase),
                IsAppPoolStarted = lines[2].Equals("Started", StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Runs a PowerShell command on a remote server using WinRM over HTTP with credentials
        /// </summary>
        private async Task<string> RunRemotePowerShellCommandAsync(string serverName, string command)
        {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
                throw new InvalidOperationException("Credentials not set. Call SetCredentials() first.");

            return await Task.Run(() =>
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

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException($"PowerShell command failed: {error}");

                return output;
            });
        }
    }
}