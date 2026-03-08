using System.Windows;

namespace IISDeployer.View
{
    /// <summary>
    /// Simple dialog for renaming a navigation tab label.
    /// </summary>
    public partial class RenameNavItemDialog : Window
    {
        /// <summary>
        /// The new label entered by the user. Null/empty if cancelled.
        /// </summary>
        public string? NewLabel { get; private set; }

        public RenameNavItemDialog(string currentLabel)
        {
            InitializeComponent();
            LabelTextBox.Text = currentLabel;
            LabelTextBox.SelectAll();
            LabelTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LabelTextBox.Text))
            {
                MessageBox.Show("Label cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewLabel = LabelTextBox.Text.Trim();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}