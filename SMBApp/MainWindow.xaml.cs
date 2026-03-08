using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SMBApp.Services;
using SMBApp.View;

namespace SMBApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Shared singleton — same instance as SettingsPage, so cache invalidation works correctly
        private readonly ConfigurationService _configurationService = ConfigurationService.Instance;

        public MainWindow()
        {
            InitializeComponent();
            BuildNavBar();
            NavigateToDefault();
        }

        /// <summary>
        /// Rebuilds the navigation bar from the persisted NavigationItems config.
        /// Called on startup and after saving nav changes in Settings.
        /// </summary>
        public void BuildNavBar()
        {
            NavBar.Children.Clear();

            var items = _configurationService.GetNavigationItems();

            foreach (var item in items.Where(i => i.IsVisible))
            {
                bool isHighlighted = item.PageKey == "DynamicDeploymentPage";

                var btn = new Button
                {
                    Content = item.Label,
                    Width = double.NaN,
                    MinWidth = 100,
                    Padding = new Thickness(12, 0, 12, 0),
                    Margin = new Thickness(5, 5, 0, 5),
                    Foreground = Brushes.White,
                    Background = isHighlighted
                        ? new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2))
                        : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold,
                    Tag = item.PageKey
                };

                btn.Click += NavButton_Click;
                NavBar.Children.Add(btn);
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pageKey)
                NavigateToPage(pageKey);
        }

        private void NavigateToDefault()
        {
            var items = _configurationService.GetNavigationItems();
            string firstKey = items.FirstOrDefault(i => i.IsVisible)?.PageKey ?? "SMBPage";
            NavigateToPage(firstKey);
        }

        private void NavigateToPage(string pageKey)
        {
            System.Windows.Controls.Page page = pageKey switch
            {
                "SMBPage"               => new SMBPage(),
                "DynamicDeploymentPage" => new DynamicDeploymentPage(),
                "SettingsPage"          => new SettingsPage(),
                _                       => new SMBPage()
            };

            MainFrame.Navigate(page);
        }
    }
}