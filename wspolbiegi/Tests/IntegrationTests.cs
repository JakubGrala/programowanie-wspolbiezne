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

        logicApi.CreatePlane(220, 160);
        logicApi.PlaceBalls(10, 14);
        logicApi.StartSimulation();

        for (int i = 0; i < 1200; i++)
        {
            logicApi.SimulationTick();
        }

        foreach (Ball ball in dataApi.GetBalls())
        {
            Assert.IsTrue(ball.X - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.X + ball.Radius <= 220 + 1e-6);
            Assert.IsTrue(ball.Y - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.Y + ball.Radius <= 160 + 1e-6);
        }
    }
}
