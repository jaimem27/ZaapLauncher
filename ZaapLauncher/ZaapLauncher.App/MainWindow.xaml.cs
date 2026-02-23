using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ZaapLauncher.App.Class;
using ZaapLauncher.App.ViewModels;

namespace ZaapLauncher.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            Loaded += (_, __) =>
            {
                ((Storyboard)FindResource("PortalBreath")).Begin();
                ((Storyboard)FindResource("VortexBrushRotate")).Begin();
            };           

        }

        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartUpdate(forceRepair: true);
        }

        private void NewsLink_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is NewsItem item && !string.IsNullOrWhiteSpace(item.Link))
                Process.Start(new ProcessStartInfo(item.Link) { UseShellExecute = true });
        }


        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }


        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://discord.gg/eGGu9ZVCG2";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

    }

}
