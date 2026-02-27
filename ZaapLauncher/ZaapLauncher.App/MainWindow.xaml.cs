using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Animation;
using ZaapLauncher.App.Class;
using ZaapLauncher.App.Services;
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

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(MainViewModel.IsReadyToPlay), StringComparison.Ordinal))
                return;

            if (!ViewModel.IsReadyToPlay)
                return;

            if (TryFindResource("PlayReadyGlowPulse") is Storyboard readyFx)
                readyFx.Begin(this);
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
            OpenDiscordCommunity();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            LogsPreviewTextBox.Text = BuildLogsPreview();
            SettingsModal.Visibility = Visibility.Visible;
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Collapsed;
        }

        private void RunRepairFromModal_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartUpdate(forceRepair: true);
            SettingsModal.Visibility = Visibility.Collapsed;
        }

        private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Paths.InstallDir);

            var gameExe = Path.Combine(Paths.InstallDir, "Dofus.exe");
            if (File.Exists(gameExe))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{gameExe}\"") { UseShellExecute = true });
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.InstallDir,
                UseShellExecute = true
            });
        }

        private void OpenCommunityDiscord_Click(object sender, RoutedEventArgs e)
        {
            OpenDiscordCommunity();
        }

        private static void OpenDiscordCommunity()
        {
            var url = "https://discord.gg/eGGu9ZVCG2";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static string BuildLogsPreview()
        {
            var candidates = new[]
            {
                Path.Combine(Paths.InstallDir, "logs"),
                Path.Combine(Paths.LauncherDataDir, "logs")
            };

            var latestLog = candidates
                .Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestLog is null)
            {
                return "No se encontraron logs en:\n"
                    + $"- {Path.Combine(Paths.InstallDir, "logs")}\n"
                    + $"- {Path.Combine(Paths.LauncherDataDir, "logs")}";
            }

            try
            {
                var lines = File.ReadAllLines(latestLog.FullName);
                var tail = lines.TakeLast(250);
                var builder = new StringBuilder();
                builder.AppendLine($"Archivo: {latestLog.FullName}");
                builder.AppendLine($"Última modificación (UTC): {latestLog.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine(new string('-', 72));

                foreach (var line in tail)
                    builder.AppendLine(line);

                return builder.ToString();
            }
            catch (Exception ex)
            {
                return $"No se pudo leer el log:\n{latestLog.FullName}\n\n{ex.GetType().Name}: {ex.Message}";
            }
        }

    }

}
