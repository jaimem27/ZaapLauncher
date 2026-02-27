using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using ZaapLauncher.App.Class;
using ZaapLauncher.App.Models;
using ZaapLauncher.App.Services;

namespace ZaapLauncher.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public enum LauncherState
    {
        Initializing,
        Checking,
        Downloading,
        Repairing,
        Ready,
        Error
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();

    private double _patchProgress;
    private double _targetPatchProgress;
    private CancellationTokenSource? _progressAnimationCts;

    public double PatchProgress
    {
        get => _patchProgress;
        private set => Set(ref _patchProgress, value);
    }

    private bool _isReadyToPlay;
    public bool IsReadyToPlay
    {
        get => _isReadyToPlay;
        private set
        {
            if (Set(ref _isReadyToPlay, value))
                ((RelayCommand)PlayCommand).NotifyCanExecuteChanged();
        }
    }

    private string _statusHeadline = "Inicializando…";
    public string StatusHeadline
    {
        get => _statusHeadline;
        private set => Set(ref _statusHeadline, value);
    }

    private string _statusDetail = "Preparando launcher.";
    public string StatusDetail
    {
        get => _statusDetail;
        private set => Set(ref _statusDetail, value);
    }

    public string ServerStatusText { get; private set; } = "Offline";
    public Brush ServerStatusColor { get; private set; } = Brushes.IndianRed;
    public string LauncherVersion { get; } = "v1.0";

    public ICommand PlayCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand CancelUpdateCommand { get; }

    private readonly UpdateOrchestrator _updater = new();
    private readonly LauncherService _launcher = new();
    private readonly Random _random = new();
    private readonly LauncherLogService _logger = new();
    private readonly NewsService _newsService = new();

    private static readonly string[] FlavorTexts =
    [
        "Sobornando al Anutrof…",
        "Negociando con el mercader de Astrub…",
        "Ajustando impuestos en Bonta…",
        "Recuperando kamas perdidas…",
        "Alimentando a los tofus…",
        "Persiguiendo tofus fugitivos…",
        "Calmando jalatós nerviosos…",
        "Despertando al Dragopavo…",
        "Estabilizando el Zaap…",
        "Alineando runas arcanas…",
        "Reforzando el anillo rúnico…",
        "Canalizando energía dimensional…",
        "Sellando grietas temporales…",
        "Invocando el portal…",
        "Compilando hechizos…",
        "Invocando servidores…",
        "Encantando archivos…",
        "Reparando el grimorio…",
        "Purificando datos corruptos…",
        "Reordenando los planos astrales…",
        "Consultando al Xelor…",
        "Esperando que el Sram deje de esconderse…",
        "Convenciendo al Zurcarák de cooperar…",
        "Pidiéndole permiso al Feca…",
        "Ajustando el destino con Ecaflip…",
        "Despertando al Yopuka…"
    ];

    private string? _lastFlavorText;

    private CancellationTokenSource? _updateCts;

    public MainViewModel()
    {
        PlayCommand = new RelayCommand(_ => Launch(), _ => IsReadyToPlay && !File.Exists(Paths.UpdateStatePath));
        RepairCommand = new RelayCommand(_ => StartUpdate(forceRepair: true));
        CancelUpdateCommand = new RelayCommand(_ => _updateCts?.Cancel(), _ => _updateCts is not null);

        _ = LoadNewsAsync();
        _ = StartUpdateAsync(forceRepair: false);
    }

    public void StartUpdate(bool forceRepair) => _ = StartUpdateAsync(forceRepair);

    private async Task StartUpdateAsync(bool forceRepair)
    {
        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();

        IsReadyToPlay = false;
        ServerStatusText = "Updating";
        ServerStatusColor = Brushes.Gold;
        OnPropertyChanged(nameof(ServerStatusText));
        OnPropertyChanged(nameof(ServerStatusColor));
        SetState(LauncherState.Initializing, "Preparando actualización...");
        await _logger.LogInfoAsync("Updater", $"Inicio de actualización. forceRepair={forceRepair}.");


        SetPatchProgressTarget(0);

        string? lastProgressEntry = null;
        var progress = new Progress<UpdateProgress>(p =>
        {
            SetPatchProgressTarget(p.Percent);
            var state = p.Stage switch
            {
                UpdateStage.FetchManifest or UpdateStage.VerifyFiles or UpdateStage.FinalCheck => LauncherState.Checking,
                UpdateStage.Downloading => LauncherState.Downloading,
                UpdateStage.Applying => LauncherState.Repairing,
                UpdateStage.Ready => LauncherState.Ready,
                _ => LauncherState.Checking
            };

            SetState(state, p.Detail);

            var progressEntry = $"Etapa={p.Stage}, Progreso={p.Percent:0.##}%, Headline='{p.Headline}', Detail='{p.Detail}'";
            if (!string.Equals(lastProgressEntry, progressEntry, StringComparison.Ordinal))
            {
                lastProgressEntry = progressEntry;
                _ = _logger.LogInfoAsync("Updater", progressEntry);
            }
        });

        try
        {
            await _updater.RunAsync(Paths.InstallDir, forceRepair, progress, _updateCts.Token);

            SetPatchProgressTarget(100);
            SetState(LauncherState.Ready);
            ServerStatusText = "Online";
            ServerStatusColor = Brushes.MediumSeaGreen;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
            await _logger.LogInfoAsync("Updater", "Actualización completada correctamente.");
        }
        catch (OperationCanceledException)
        {
            SetState(LauncherState.Error, "Puedes reanudar cuando quieras.");
            StatusHeadline = "Actualización cancelada.";
            ServerStatusText = "Paused";
            ServerStatusColor = Brushes.Orange;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
            await _logger.LogWarningAsync("Updater", "Actualización cancelada por el usuario.");
        }
        catch (UpdateFlowException ex)
        {
            SetState(LauncherState.Error, ex.Detail);
            StatusHeadline = ex.Headline;
            SetPatchProgressTarget(Math.Min(PatchProgress, 98));
            ServerStatusText = "Error";
            ServerStatusColor = Brushes.OrangeRed;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
            await _logger.LogErrorAsync("Updater", $"Error de actualización: {ex.Headline} - {ex.Detail}", ex);
        }
        catch (Exception ex)
        {
            SetState(LauncherState.Error, $"{ex.GetType().Name}: {ex.Message}");
            StatusHeadline = "Ha ocurrido un error en el portal.";
            ServerStatusText = "Offline";
            ServerStatusColor = Brushes.IndianRed;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
            await _logger.LogErrorAsync("Updater", "Error inesperado durante el flujo de actualización.", ex);
        }
    }

    private void SetState(LauncherState state, string detail = "")
    {
        switch (state)
        {
            case LauncherState.Initializing:
                StatusHeadline = GetRandomFlavorText();
                StatusDetail = string.IsNullOrWhiteSpace(detail) ? "Preparando launcher." : detail;
                IsReadyToPlay = false;
                break;
            case LauncherState.Checking:
                StatusHeadline = GetRandomFlavorText();
                StatusDetail = string.IsNullOrWhiteSpace(detail) ? "Verificando archivos..." : detail;
                IsReadyToPlay = false;
                break;
            case LauncherState.Downloading:
                StatusHeadline = GetRandomFlavorText();
                StatusDetail = string.IsNullOrWhiteSpace(detail) ? "Descargando recursos..." : detail;
                IsReadyToPlay = false;
                break;
            case LauncherState.Repairing:
                StatusHeadline = GetRandomFlavorText();
                StatusDetail = string.IsNullOrWhiteSpace(detail) ? "Aplicando reparación..." : detail;
                IsReadyToPlay = false;
                break;
            case LauncherState.Ready:
                StatusHeadline = "Portal estabilizado. Listo para entrar.";
                StatusDetail = "Todo actualizado. Buen viaje, aventurero.";
                IsReadyToPlay = true;
                break;
            case LauncherState.Error:
                StatusDetail = string.IsNullOrWhiteSpace(detail) ? "Se produjo un error durante la actualización." : detail;
                IsReadyToPlay = false;
                break;
        }
    }

    private string GetRandomFlavorText()
    {
        if (FlavorTexts.Length == 0)
            return "Estabilizando el Zaap…";

        string selected;
        do
        {
            selected = FlavorTexts[_random.Next(FlavorTexts.Length)];
        }
        while (FlavorTexts.Length > 1 && string.Equals(selected, _lastFlavorText, StringComparison.Ordinal));

        _lastFlavorText = selected;
        return selected;
    }

    private void SetPatchProgressTarget(double value)
    {
        _targetPatchProgress = Math.Clamp(value, 0, 100);

        _progressAnimationCts?.Cancel();
        _progressAnimationCts?.Dispose();
        _progressAnimationCts = new CancellationTokenSource();

        _ = AnimatePatchProgressAsync(_progressAnimationCts.Token);
    }

    private async Task AnimatePatchProgressAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var current = PatchProgress;
                var delta = _targetPatchProgress - current;
                if (Math.Abs(delta) < 0.1)
                {
                    PatchProgress = _targetPatchProgress;
                    break;
                }

                var step = Math.Max(0.25, Math.Abs(delta) * 0.2);
                PatchProgress = current + Math.Sign(delta) * step;
                await Task.Delay(16, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // no-op
        }
    }

    private void Launch()
    {
        if (File.Exists(Paths.UpdateStatePath))
        {
            _ = _logger.LogWarningAsync("Launcher", "Se intentó abrir el juego con una actualización pendiente de recuperar.");
            throw new InvalidOperationException("Hay una actualización pendiente de recuperar. Ejecuta reparar/actualizar antes de jugar.");
        }

        var gameExe = Path.Combine(Paths.InstallDir, "Dofus.exe");
        if (!File.Exists(gameExe))
        {
            _ = _logger.LogErrorAsync("Launcher", $"No se encontró ejecutable del juego en: {gameExe}");
            throw new FileNotFoundException("No se encontró el ejecutable del juego.", gameExe);
        }

        _ = _logger.LogInfoAsync("Launcher", $"Iniciando juego: {gameExe}");

        _launcher.Launch(gameExe);
    }

    private async Task LoadNewsAsync()
    {
        try
        {
            var root = await _newsService.FetchAsync(CancellationToken.None);

            NewsItems.Clear();
            if (root?.Items is null)
                return;

            foreach (var item in root.Items)
                NewsItems.Add(item);
        }
        catch
        {
            // ignore: launcher can run without news
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}