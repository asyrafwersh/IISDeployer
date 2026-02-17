using System.Windows;
using SMBApp.View;

namespace SMBApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new SMBPage());
        }

        private void NavSMBPage_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SMBPage());
        }

        private void NavReturnsDeploymentPage_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ReturnsDeploymentPage());
        }
    }
}