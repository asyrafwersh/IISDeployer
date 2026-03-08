namespace SMBApp.Models
{
    /// <summary>
    /// Root configuration model for appsettings.json
    /// </summary>
    public class AppSettings
    {
        public List<NetworkConfiguration> NetworkConfigurations { get; set; } = new();
        public DeploymentSettings DeploymentSettings { get; set; } = new();

        /// <summary>
        /// Ordered list of navigation items shown in the main window header bar.
        /// </summary>
        public List<NavigationItem> NavigationItems { get; set; } = new();
    }
}