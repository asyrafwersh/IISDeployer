using System.Collections.Generic;

namespace IISDeployer.Models
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

        /// <summary>
        /// Configuration-specific deployment settings
        /// </summary>
        public ConfigurationDeploymentSettings DeploymentSettings { get; set; } = new();
    }

    /// <summary>
    /// Deployment settings specific to a network configuration
    /// </summary>
    public class ConfigurationDeploymentSettings
    {
        // Legacy flat properties kept for backwards-compat deserialization
        public string ParentSourceFolder { get; set; } = string.Empty;
        public List<string> SelectedPaths { get; set; } = new();

        /// <summary>
        /// Frontend-specific paths for this configuration (legacy, kept for backwards compat)
        /// </summary>
        public ConfigurationSectionPaths Frontend { get; set; } = new();

        /// <summary>
        /// API-specific paths for this configuration (legacy, kept for backwards compat)
        /// </summary>
        public ConfigurationSectionPaths Api { get; set; } = new();

        /// <summary>
        /// Dynamic deployment sections — replaces the hardcoded Frontend/Api split.
        /// Each section is fully configurable by the user.
        /// </summary>
        public List<DeploymentSectionConfig> Sections { get; set; } = new();
    }

    /// <summary>
    /// Per-section source folder, selected paths, and IIS website name for a network configuration
    /// </summary>
    public class ConfigurationSectionPaths
    {
        public string ParentSourceFolder { get; set; } = string.Empty;
        public List<string> SelectedPaths { get; set; } = new();

        /// <summary>
        /// The IIS website name associated with this network configuration section
        /// </summary>
        public string IISWebsiteName { get; set; } = string.Empty;
    }
}