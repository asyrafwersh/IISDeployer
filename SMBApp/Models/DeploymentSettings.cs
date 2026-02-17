namespace SMBApp.Models
{
    /// <summary>
    /// Represents the deployment settings for Returns Frontend and Returns API
    /// </summary>
    public class DeploymentSettings
    {
        public FrontendDeploymentSettings Frontend { get; set; } = new();
        public ApiDeploymentSettings Api { get; set; } = new();
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
        /// The IIS website name (e.g., DKSHFrontend_UAT)
        /// </summary>
        public string IISWebsiteName { get; set; } = "DKSHFrontend_UAT";

        /// <summary>
        /// The environment file name to backup (e.g., .env)
        /// </summary>
        public string EnvFileName { get; set; } = ".env";

        /// <summary>
        /// The destination subfolder on the remote server for deployment files (e.g., Downloads)
        /// </summary>
        public string DestinationSubFolder { get; set; } = string.Empty;
    }

    /// <summary>
    /// API-specific deployment settings (placeholder for future use)
    /// </summary>
    public class ApiDeploymentSettings
    {
        public string ParentSourceFolder { get; set; } = string.Empty;
        public List<string> SelectedPaths { get; set; } = new();
        public string IISWebsiteName { get; set; } = string.Empty;
    }
}