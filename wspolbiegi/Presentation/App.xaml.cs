using System.Windows;
using Data;
using Logic;

namespace Presentation
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            IBallRepository repository = new InMemoryBallRepository();
            IDataApi dataApi = new DataApi(repository);
            IRandomProvider randomProvider = new DefaultRandomProvider();
            ILogicApi logicApi = new LogicApi(dataApi, randomProvider);
            MainViewModel viewModel = new(logicApi);

            MainWindow mainWindow = new(viewModel);
            mainWindow.Show();
        }
    }
}
