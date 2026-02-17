namespace SMBApp.Models
{
    /// <summary>
    /// Root configuration model for appsettings.json
    /// </summary>
    public class AppSettings
    {
        public List<NetworkConfiguration> NetworkConfigurations { get; set; } = new();
        public DeploymentSettings DeploymentSettings { get; set; } = new();
    }
}