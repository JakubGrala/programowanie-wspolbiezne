using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Logic;
using Presentation.Model;

namespace Presentation.ViewModel;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public const double LogicalPlaneWidth = 700;

    public const double LogicalPlaneHeight = 380;

    public const double BallRadius = 18;

    private readonly ILogicApi logicApi;
    private double lastHostWidth;
    private double lastHostHeight;
    private string ballCountText = "8";
    private bool hasBalls;
    private IReadOnlyCollection<LogicBall>? lastBalls;

    public MainViewModel(ILogicApi logicApi)
    {
        this.logicApi = logicApi ?? throw new ArgumentNullException(nameof(logicApi));
        SimulationModel = new MainSimulationModel(LogicalPlaneWidth, LogicalPlaneHeight);

        ApplyBallsCommand = new AsyncRelayCommand(ApplyBallsFromInputAsync, () => !logicApi.IsSimulationRunning);
        StartSimulationCommand = new AsyncRelayCommand(StartSimulationAsync, CanStartSimulation);
        StopSimulationCommand = new AsyncRelayCommand(StopSimulationAsync, () => logicApi.IsSimulationRunning);

        this.logicApi.BallsUpdated += OnBallsUpdated;

        _ = Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = ApplyBallsFromInputAsync()), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainSimulationModel SimulationModel { get; }

    public string BallCountText
    {
        get => ballCountText;
        set
        {
            if (ballCountText == value)
            {
                return;
            }

            ballCountText = value;
            OnPropertyChanged();
        }
    }

    public ICommand ApplyBallsCommand { get; }

    public ICommand StartSimulationCommand { get; }

    public ICommand StopSimulationCommand { get; }

    public void OnPlayfieldHostSizeChanged(double width, double height)
    {
        lastHostWidth = width;
        lastHostHeight = height;

        if (lastBalls is not null)
        {
            RefreshVisualization(lastBalls);
        }
    }

    public void Dispose()
    {
        logicApi.BallsUpdated -= OnBallsUpdated;
        _ = StopSimulationAsync();
    }

    private void OnBallsUpdated(object? sender, BallsUpdatedEventArgs e)
    {
        _ = Application.Current.Dispatcher.BeginInvoke(() =>
        {
            RefreshVisualization(e.Balls);
        });
    }

    private bool CanStartSimulation() => !logicApi.IsSimulationRunning && hasBalls;

    private async Task ApplyBallsFromInputAsync()
    {
        if (logicApi.IsSimulationRunning)
        {
            return;
        }

        if (!int.TryParse(BallCountText, out int count) || count <= 0)
        {
            return;
        }

        await logicApi.CreatePlaneAsync(LogicalPlaneWidth, LogicalPlaneHeight).ConfigureAwait(true);
        IReadOnlyCollection<LogicBall> balls = await logicApi.PlaceBallsAsync(count, BallRadius).ConfigureAwait(true);
        hasBalls = balls.Count > 0;
        RefreshVisualization(balls);
        InvalidateCommands();
    }

    private async Task StartSimulationAsync()
    {
        await logicApi.StartSimulationAsync().ConfigureAwait(true);
        InvalidateCommands();
    }

    private async Task StopSimulationAsync()
    {
        await logicApi.StopSimulationAsync().ConfigureAwait(true);
        InvalidateCommands();
    }

    private void RefreshVisualization(IReadOnlyCollection<LogicBall> balls)
    {
        lastBalls = balls;
        double width = lastHostWidth > 0 ? lastHostWidth : LogicalPlaneWidth;
        double height = lastHostHeight > 0 ? lastHostHeight : LogicalPlaneHeight;
        SimulationModel.SyncFromLogic(balls, width, height);
    }

    private void InvalidateCommands() => CommandManager.InvalidateRequerySuggested();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
