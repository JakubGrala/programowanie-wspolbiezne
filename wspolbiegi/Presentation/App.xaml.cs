using System.Windows;
using Data;
using Logic;
using Presentation.ViewModel;

namespace Presentation;

public partial class App : Application
{
    private MainViewModel? mainViewModel;
    private DataApi? dataApi;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        dataApi = new DataApi();
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
        dataApi?.Dispose();
        base.OnExit(e);
    }
}
