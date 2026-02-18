using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ZaapLauncher.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new
            {
                ServerStatusText = "Offline",
                ServerStatusColor = System.Windows.Media.Brushes.IndianRed,

                ServerStatusGlowColor = System.Windows.Media.Colors.IndianRed,

                NewsText = "• Patch notes will appear here.\n• Keep it short and clean.\n\nZaapLauncher v1.0 is initializing…",

                StatusTitle = "Zaap node unreachable",
                StatusSubtitle = "Check connection or try Repair."
            };
            Loaded += (_, __) =>
            {
                var breath = (Storyboard)FindResource("PortalBreath");
                breath.Begin();

                var vortex = (Storyboard)FindResource("VortexBrushRotate");
                vortex.Begin();


            };


        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://discord.com/invite/drakonheart";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

    }

}
