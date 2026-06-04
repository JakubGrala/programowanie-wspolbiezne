using Data;
using Logic;

namespace Tests;

[TestClass]
public sealed class IntegrationTests
{
    [TestMethod]
    public void DataAndLogic_Together_ShouldRunSimulationWithoutLeavingPlane()
    {
        IDataApi dataApi = new DataApi(new InMemoryBallRepository());
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlaneAsync(220, 160).GetAwaiter().GetResult();
        logicApi.PlaceBallsAsync(10, 14).GetAwaiter().GetResult();

        logicApi.StartSimulationAsync().GetAwaiter().GetResult();
        System.Threading.Thread.Sleep(500);
        logicApi.StopSimulationAsync().GetAwaiter().GetResult();

        foreach (BallSnapshot ball in dataApi.GetSnapshots())
        {
            Assert.IsTrue(ball.X - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.X + ball.Radius <= 220 + 1e-6);
            Assert.IsTrue(ball.Y - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.Y + ball.Radius <= 160 + 1e-6);
        }
    }
}
