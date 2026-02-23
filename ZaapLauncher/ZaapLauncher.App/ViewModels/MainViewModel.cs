using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

    private CancellationTokenSource? _updateCts;

    public MainViewModel()
    {
        PlayCommand = new RelayCommand(_ => Launch(), _ => IsReadyToPlay);
        RepairCommand = new RelayCommand(_ => StartUpdate(forceRepair: true));
        CancelUpdateCommand = new RelayCommand(_ => _updateCts?.Cancel(), _ => _updateCts is not null);

        LoadNewsFromDisk();
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

        SetPatchProgressTarget(0);

        var progress = new Progress<UpdateProgress>(p =>
        {
            SetPatchProgressTarget(p.Percent);
            StatusHeadline = p.Headline;
            StatusDetail = p.Detail;
        });

        try
        {
            await _updater.RunAsync(Paths.InstallDir, forceRepair, progress, _updateCts.Token);

            StatusHeadline = "Portal estabilizado. Listo para entrar.";
            StatusDetail = "Todo actualizado. Buen viaje, aventurero.";
            SetPatchProgressTarget(100);
            IsReadyToPlay = true;
            ServerStatusText = "Online";
            ServerStatusColor = Brushes.MediumSeaGreen;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
        }
        catch (OperationCanceledException)
        {
            StatusHeadline = "Actualización cancelada.";
            StatusDetail = "Puedes reanudar cuando quieras.";
            ServerStatusText = "Paused";
            ServerStatusColor = Brushes.Orange;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
        }
        catch (Exception ex)
        {
            StatusHeadline = "Ha ocurrido un error en el portal.";
            StatusDetail = ex.Message;
            IsReadyToPlay = false;
            ServerStatusText = "Offline";
            ServerStatusColor = Brushes.IndianRed;
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
        }
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
        var gameExe = Path.Combine(Paths.InstallDir, "Dofus.exe");
        if (!File.Exists(gameExe))
            throw new FileNotFoundException("No se encontró el ejecutable del juego.", gameExe);

        _launcher.Launch(gameExe);
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