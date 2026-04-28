using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Logic;
using Presentation.Model;

namespace Presentation.ViewModel;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    public const double LogicalPlaneWidth = 700;

    public const double LogicalPlaneHeight = 380;

    public const double BallRadius = 18;

    private readonly ILogicApi logicApi;
    private readonly DispatcherTimer simulationTimer;
    private double lastHostWidth;
    private double lastHostHeight;
    private string ballCountText = "8";

    public MainViewModel(ILogicApi logicApi)
    {
        this.logicApi = logicApi ?? throw new ArgumentNullException(nameof(logicApi));
        SimulationModel = new MainSimulationModel(LogicalPlaneWidth, LogicalPlaneHeight);

        ApplyBallsCommand = new RelayCommand(ApplyBallsFromInput, () => !logicApi.IsSimulationRunning);
        StartSimulationCommand = new RelayCommand(StartSimulation, CanStartSimulation);
        StopSimulationCommand = new RelayCommand(StopSimulation, () => logicApi.IsSimulationRunning);

        simulationTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        simulationTimer.Tick += OnSimulationTimerTick;

        _ = Application.Current.Dispatcher.BeginInvoke(new Action(ApplyBallsFromInput), DispatcherPriority.Loaded);
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
        RefreshVisualization();
    }

    public void Dispose()
    {
        simulationTimer.Stop();
        simulationTimer.Tick -= OnSimulationTimerTick;
        logicApi.StopSimulation();
    }

    private bool CanStartSimulation() =>
        !logicApi.IsSimulationRunning && logicApi.GetBalls().Count > 0;

    private void ApplyBallsFromInput()
    {
        if (logicApi.IsSimulationRunning)
        {
            return;
        }

        if (!int.TryParse(BallCountText, out int count) || count <= 0)
        {
            return;
        }

        logicApi.CreatePlane(LogicalPlaneWidth, LogicalPlaneHeight);
        logicApi.PlaceBalls(count, BallRadius);
        RefreshVisualization();
        InvalidateCommands();
    }

    private void StartSimulation()
    {
        logicApi.StartSimulation();
        simulationTimer.Start();
        InvalidateCommands();
    }

    private void StopSimulation()
    {
        simulationTimer.Stop();
        logicApi.StopSimulation();
        InvalidateCommands();
    }

    private void OnSimulationTimerTick(object? sender, EventArgs e)
    {
        logicApi.SimulationTick();
        RefreshVisualization();
    }

    private void RefreshVisualization()
    {
        double width = lastHostWidth > 0 ? lastHostWidth : LogicalPlaneWidth;
        double height = lastHostHeight > 0 ? lastHostHeight : LogicalPlaneHeight;
        SimulationModel.SyncFromLogic(logicApi.GetBalls(), width, height);
    }

    private void InvalidateCommands() => CommandManager.InvalidateRequerySuggested();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
