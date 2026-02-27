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
            Loaded += MainWindow_Loaded;


        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var minSplashDuration = TimeSpan.FromMilliseconds(2500);
            var start = Stopwatch.StartNew();

            await WaitForInitialPhaseAsync();

            var remaining = minSplashDuration - start.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);

            HideSplashOverlay();
        }

        private async Task WaitForInitialPhaseAsync()
        {
            var maxWait = TimeSpan.FromSeconds(8);
            var waitStart = Stopwatch.StartNew();

            while (ViewModel.StatusHeadline == "Inicializando…" && waitStart.Elapsed < maxWait)
                await Task.Delay(120);
        }

        private void HideSplashOverlay()
        {
            (TryFindResource("VortexLoop") as Storyboard)?.Stop(this);

            if (TryFindResource("HideSplash") is not Storyboard storyboard)
            {
                SplashOverlay.Visibility = Visibility.Collapsed;
                SplashOverlay.IsHitTestVisible = false;
                return;
            }

            void OnCompleted(object? _, EventArgs __)
            {
                storyboard.Completed -= OnCompleted;
                SplashOverlay.Visibility = Visibility.Collapsed;
                SplashOverlay.IsHitTestVisible = false;
            }

            storyboard.Completed += OnCompleted;
            storyboard.Begin(this);
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
