using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZaapLauncher.App.Class;

namespace ZaapLauncher.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<NewsItem> NewsItems { get; } = new();

        public string ServerStatusText { get; set; } = "Offline";
        public Brush ServerStatusColor { get; set; } = Brushes.IndianRed;
        public Color ServerStatusGlowColor { get; set; } = Colors.IndianRed;

        public string NewsText { get; set; } =
            "• Patch notes will appear here.\n• Keep it short and clean.\n\nZaapLauncher v1.0 is initializing…";

        public string StatusTitle { get; set; } = "Zaap no esta funcionando correctamente.";
        public string StatusSubtitle { get; set; } = "Comprueba la conexión a internet o repara el cliente.";
        public string LauncherVersion { get; set; } = "v1.0";

        private int _progressValue = 0; // 0..100
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(ProgressFillWidth)); }
        }

        // ancho visual de la barra (tiene que coincidir con el Width del Grid)
        public double ProgressFillWidth => (420 - 2) * (ProgressValue / 100.0); // -2 por padding/border aprox

        public string ProgressText => $"{ProgressValue}%";

        public bool ShowRepair => ServerStatusText == "Offline"; // o tu lógica real

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            LoadNewsFromDisk();

            Loaded += (_, __) =>
            {
                ((Storyboard)FindResource("PortalBreath")).Begin();
                ((Storyboard)FindResource("VortexBrushRotate")).Begin();

                // demo
                StartFakeProgress();
            };           

        }

        private async void StartFakeProgress()
        {
            for (int i = 0; i <= 100; i += 2)
            {
                ProgressValue = i;
                await Task.Delay(60);
            }
        }

        private void Repair_Click(object sender, RoutedEventArgs e)
        {
            // aquí luego pones tu lógica real:
            // - revalidar archivos
            // - limpiar cache
            // - relanzar node / updater
            MessageBox.Show("Repair clicked");
        }

        private void LoadNewsFromDisk()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, "Assets", "news", "news.json");

                if (!File.Exists(path))
                    return;

                var json = File.ReadAllText(path);

                var root = JsonSerializer.Deserialize<NewsRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                NewsItems.Clear();
                if (root?.Items == null) return;

                foreach (var item in root.Items)
                    NewsItems.Add(item);
            }
            catch
            {
                // opcional: log
            }
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
