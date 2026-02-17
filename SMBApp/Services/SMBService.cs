using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace SMBApp.Services
{
    /// <summary>
    /// Service for connecting to SMB network shares
    /// </summary>
    public class SMBService
    {
        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(ref NetResource netResource, string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);

        [StructLayout(LayoutKind.Sequential)]
        private struct NetResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string? LocalName;
            public string? RemoteName;
            public string? Comment;
            public string? Provider;
        }

        private const int RESOURCETYPE_DISK = 0x00000001;
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;

        private bool _isConnected = false;
        private string? _lastConnectedPath = null;

        /// <summary>
        /// Connects to an SMB network share
        /// </summary>
        /// <param name="networkPath">UNC path to the network share (e.g., \\server\share)</param>
        /// <param name="username">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        /// <returns>True if connection successful, false otherwise</returns>
        public bool ConnectToShare(string networkPath, string username, string password)
        {
            try
            {
                // Extract server name from UNC path (e.g., \\DESKTOP-3GO5301 from \\DESKTOP-3GO5301\MiCCTV)
                string serverPath = GetServerPath(networkPath);
                
                // Try to disconnect from the server first to clear any existing connections
                try
                {
                    WNetCancelConnection2(serverPath, 0, true);
                }
                catch
                {
                    // Ignore errors if there was no connection to disconnect
                }

                // Also try to disconnect from the specific share path
                try
                {
                    WNetCancelConnection2(networkPath, 0, true);
                }
                catch
                {
                    // Ignore errors if there was no connection to disconnect
                }
                
                var netResource = new NetResource
                {
                    Scope = 2,
                    Type = RESOURCETYPE_DISK,
                    DisplayType = 3,
                    Usage = 1,
                    RemoteName = networkPath,
                    LocalName = null
                };

                int result = WNetAddConnection2(ref netResource, password, username, 0);

                if (result == 0)
                {
                    _isConnected = true;
                    _lastConnectedPath = networkPath;
                    return true;
                }
                else
                {
                    throw new Win32Exception(result);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to connect to {networkPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts the server path from a UNC path
        /// </summary>
        /// <param name="uncPath">Full UNC path (e.g., \\server\share\folder)</param>
        /// <returns>Server path (e.g., \\server)</returns>
        private string GetServerPath(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath) || !uncPath.StartsWith(@"\\"))
                return uncPath;

            // Remove leading \\
            string pathWithoutPrefix = uncPath.Substring(2);
            
            // Find the first backslash after the server name
            int slashIndex = pathWithoutPrefix.IndexOf('\\');
            
            if (slashIndex > 0)
            {
                // Return \\servername
                return @"\\" + pathWithoutPrefix.Substring(0, slashIndex);
            }
            
            // If no slash found, the whole thing is the server
            return uncPath;
        }

        /// <summary>
        /// Disconnects from an SMB network share
        /// </summary>
        /// <param name="networkPath">UNC path to the network share</param>
        /// <returns>True if disconnection successful, false otherwise</returns>
        public bool DisconnectFromShare(string networkPath)
        {
            try
            {
                // Disconnect from the specific share
                int result = WNetCancelConnection2(networkPath, 0, true);
                
                // Also try to disconnect from the server
                string serverPath = GetServerPath(networkPath);
                WNetCancelConnection2(serverPath, 0, true);
                
                if (result == 0)
                {
                    _isConnected = false;
                    _lastConnectedPath = null;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to disconnect from {networkPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Tests if a network path is accessible
        /// </summary>
        /// <param name="networkPath">UNC path to test</param>
        /// <returns>True if accessible, false otherwise</returns>
        public bool TestConnection(string networkPath)
        {
            try
            {
                return Directory.Exists(networkPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets list of files in the network directory
        /// </summary>
        /// <param name="networkPath">UNC path to the directory</param>
        /// <returns>Array of file paths</returns>
        public string[] GetFiles(string networkPath)
        {
            try
            {
                return Directory.GetFiles(networkPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get files from {networkPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets list of directories in the network directory
        /// </summary>
        /// <param name="networkPath">UNC path to the directory</param>
        /// <returns>Array of directory paths</returns>
        public string[] GetDirectories(string networkPath)
        {
            try
            {
                return Directory.GetDirectories(networkPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get directories from {networkPath}: {ex.Message}", ex);
            }
        }
    }
}