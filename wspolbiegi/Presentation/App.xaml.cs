using System.Windows;
using Data;
using Logic;
using Presentation.ViewModel;

namespace Presentation;

public partial class App : Application
{
    private MainViewModel? mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IBallRepository repository = new InMemoryBallRepository();
        IDataApi dataApi = new DataApi(repository);
        IRandomProvider randomProvider = new DefaultRandomProvider();
        ILogicApi logicApi = new LogicApi(dataApi, randomProvider);
        mainViewModel = new MainViewModel(logicApi);

        MainWindow mainWindow = new()
        {
            DataContext = mainViewModel,
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        mainViewModel?.Dispose();
        base.OnExit(e);
    }
}
