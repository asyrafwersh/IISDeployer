using System.Windows;
using SMBApp.Models;

namespace SMBApp.View
{
    /// <summary>
    /// Dialog to collect section details when adding a new deployment section
    /// </summary>
    public partial class AddSectionDialog : Window
    {
        /// <summary>
        /// The resulting section configuration if the user clicks Add
        /// </summary>
        public DeploymentSectionConfig? Result { get; private set; }

        public AddSectionDialog()
        {
            InitializeComponent();
            SectionKeyTextBox.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string key = SectionKeyTextBox.Text.Trim();
            string title = SectionTitleTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Section Key is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SectionKeyTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Section Title is required.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SectionTitleTextBox.Focus();
                return;
            }

            Result = new DeploymentSectionConfig
            {
                SectionKey = key,
                SectionTitle = title,
                IISWebsiteName = IISWebsiteTextBox.Text.Trim(),
                EnvFileName = EnvFileNameTextBox.Text.Trim()
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
