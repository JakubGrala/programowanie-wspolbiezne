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

        for (int i = 0; i < 1200; i++)
        {
            logicApi.SimulationStep(1.0 / 60.0);
        }

        foreach (BallSnapshot ball in dataApi.GetSnapshots())
        {
            Assert.IsTrue(ball.X - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.X + ball.Radius <= 220 + 1e-6);
            Assert.IsTrue(ball.Y - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.Y + ball.Radius <= 160 + 1e-6);
        }
    }
}
