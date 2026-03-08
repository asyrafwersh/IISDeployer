namespace SMBApp.Models
{
    /// <summary>
    /// Represents the deployment settings for Frontend and API
    /// </summary>
    public class DeploymentSettings
    {
        public FrontendDeploymentSettings Frontend { get; set; } = new();
        public ApiDeploymentSettings Api { get; set; } = new();

        /// <summary>
        /// Dynamic deployment sections — replaces the hardcoded Frontend/Api split.
        /// </summary>
        public List<DeploymentSectionConfig> Sections { get; set; } = new();
    }

    /// <summary>
    /// Frontend-specific deployment settings
    /// </summary>
    public class FrontendDeploymentSettings
    {
        /// <summary>
        /// The parent source folder path for the frontend project
        /// </summary>
        public string ParentSourceFolder { get; set; } = string.Empty;

        /// <summary>
        /// List of selected file/folder relative paths to deploy
        /// </summary>
        public List<string> SelectedPaths { get; set; } = new();

        /// <summary>
        /// The IIS website name (e.g., UATFrontend)
        /// </summary>
        public string IISWebsiteName { get; set; } = "UATFrontend";

        /// <summary>
        /// The environment file name to backup (e.g., .env)
        /// </summary>
        public string EnvFileName { get; set; } = ".env";
    }

    /// <summary>
    /// API-specific deployment settings
    /// </summary>
    public class ApiDeploymentSettings
    {
        public string ParentSourceFolder { get; set; } = string.Empty;
        public List<string> SelectedPaths { get; set; } = new();

        /// <summary>
        /// The IIS website name for the API (e.g., UATApi)
        /// </summary>
        public string IISWebsiteName { get; set; } = "UATApi";

        /// <summary>
        /// The environment/config file name to backup (e.g., appsettings.json)
        /// </summary>
        public string EnvFileName { get; set; } = "appsettings.json";
    }
}