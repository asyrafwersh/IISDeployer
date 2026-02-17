namespace SMBApp.Models
{
    /// <summary>
    /// Represents a network configuration with connection details
    /// </summary>
    public class NetworkConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string NetworkPath { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}