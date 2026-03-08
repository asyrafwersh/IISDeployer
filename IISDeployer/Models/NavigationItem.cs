namespace IISDeployer.Models
{
    /// <summary>
    /// Represents a configurable navigation tab in the main window header bar.
    /// </summary>
    public class NavigationItem
    {
        /// <summary>
        /// Internal page key used to resolve which page to navigate to.
        /// Supported values: SMBPage, DynamicDeploymentPage, SettingsPage
        /// </summary>
        public string PageKey { get; set; } = string.Empty;

        /// <summary>
        /// The user-visible label shown on the navigation button.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Whether this nav item is visible in the header bar.
        /// </summary>
        public bool IsVisible { get; set; } = true;
    }
}