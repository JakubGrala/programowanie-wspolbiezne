using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Logic;

namespace Presentation;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ILogicApi logicApi;

    public MainViewModel(ILogicApi logicApi)
    {
        this.logicApi = logicApi ?? throw new ArgumentNullException(nameof(logicApi));
        GenerateBallsCommand = new RelayCommand(GenerateBalls);
        GenerateBalls();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BallViewModel> Balls { get; } = [];

    public ICommand GenerateBallsCommand { get; }

    public double PlaneWidth => 700;

    public double PlaneHeight => 380;

    private void GenerateBalls()
    {
        logicApi.CreatePlane(PlaneWidth, PlaneHeight);
        IReadOnlyCollection<LogicBall> balls = logicApi.PlaceBalls(count: 12, radius: 20);

        Balls.Clear();
        foreach (LogicBall ball in balls)
        {
            Balls.Add(new BallViewModel(ball));
        }

        OnPropertyChanged(nameof(Balls));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class BallViewModel
{
    public BallViewModel(LogicBall ball)
    {
        Radius = ball.Radius;
        Diameter = ball.Radius * 2;
        Left = ball.X - ball.Radius;
        Top = ball.Y - ball.Radius;
    }

    public double Radius { get; }

    public double Diameter { get; }

    public double Left { get; }

    public double Top { get; }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action execute;

    public RelayCommand(Action execute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
