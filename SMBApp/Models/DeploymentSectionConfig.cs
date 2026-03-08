namespace SMBApp.Models
{
    /// <summary>
    /// Represents a configurable deployment section that can be dynamically added per connection.
    /// Replaces the hardcoded Frontend/Api split with a flexible, user-defined section model.
    /// </summary>
    public class DeploymentSectionConfig
    {
        /// <summary>
        /// Unique key for this section (e.g., "Frontend", "Api", "Scheduler").
        /// Used in folder naming and identification.
        /// </summary>
        public string SectionKey { get; set; } = string.Empty;

        /// <summary>
        /// Display title for the section (e.g., "Frontend Section")
        /// </summary>
        public string SectionTitle { get; set; } = string.Empty;

        /// <summary>
        /// The parent source folder path for this section
        /// </summary>
        public string ParentSourceFolder { get; set; } = string.Empty;

        /// <summary>
        /// List of selected file/folder paths to deploy
        /// </summary>
        public List<string> SelectedPaths { get; set; } = new();

        /// <summary>
        /// The IIS website name associated with this section
        /// </summary>
        public string IISWebsiteName { get; set; } = string.Empty;

        /// <summary>
        /// The environment/config file name to backup during deployment (e.g., ".env", "appsettings.json")
        /// </summary>
        public string EnvFileName { get; set; } = string.Empty;
    }
}
