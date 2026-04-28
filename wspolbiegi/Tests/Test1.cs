using Data;
using Logic;

namespace Tests;

[TestClass]
public sealed class DataApiTests
{
    [TestMethod]
    public void CreateBall_ShouldStoreBallWithExpectedValues()
    {
        IDataApi api = new DataApi(new InMemoryBallRepository());

        Ball created = api.CreateBall(10, 20, 5);

        Assert.AreEqual(10, created.X);
        Assert.AreEqual(20, created.Y);
        Assert.AreEqual(5, created.Radius);
        Assert.AreEqual(1, api.GetBalls().Count);
    }

    [TestMethod]
    public void CreateBall_WithInvalidRadius_ShouldThrow()
    {
        IDataApi api = new DataApi(new InMemoryBallRepository());

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => api.CreateBall(0, 0, 0));
    }

    [TestMethod]
    public void UpdateBall_ShouldChangePosition()
    {
        IDataApi api = new DataApi(new InMemoryBallRepository());
        Ball ball = api.CreateBall(1, 2, 3);

        api.UpdateBall(ball, 9, 8);

        Assert.AreEqual(9, ball.X);
        Assert.AreEqual(8, ball.Y);
    }

    [TestMethod]
    public void UpdateBall_WithUnknownBall_ShouldThrow()
    {
        IDataApi api = new DataApi(new InMemoryBallRepository());
        Ball foreign = new(1, 2, 3);

        Assert.ThrowsException<InvalidOperationException>(() => api.UpdateBall(foreign, 0, 0));
    }
}

[TestClass]
public sealed class LogicApiTests
{
    [TestMethod]
    public void PlaceBalls_ShouldCreateRequestedCountInsidePlaneBounds()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlane(100, 80);
        IReadOnlyCollection<LogicBall> balls = logicApi.PlaceBalls(count: 5, radius: 10);

        Assert.AreEqual(5, balls.Count);
        foreach (LogicBall ball in balls)
        {
            Assert.IsTrue(ball.X >= 10 && ball.X <= 90);
            Assert.IsTrue(ball.Y >= 10 && ball.Y <= 70);
            Assert.AreEqual(10, ball.Radius);
        }
    }

    [TestMethod]
    public void PlaceBalls_WithoutPlane_ShouldThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        Assert.ThrowsException<InvalidOperationException>(() => logicApi.PlaceBalls(count: 2, radius: 5));
    }

    [TestMethod]
    public void CreatePlane_WithInvalidWidth_ShouldThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => logicApi.CreatePlane(0, 10));
    }

    [TestMethod]
    public void PlaceBalls_WithInvalidCount_ShouldThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlane(100, 80);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => logicApi.PlaceBalls(count: 0, radius: 10));
    }

    [TestMethod]
    public void StartSimulation_WithoutBalls_ShouldThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlane(100, 80);

        Assert.ThrowsException<InvalidOperationException>(() => logicApi.StartSimulation());
    }

    [TestMethod]
    public void SimulationTick_WhenStopped_ShouldNotThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlane(100, 80);
        logicApi.PlaceBalls(1, 10);

        logicApi.SimulationTick();
    }

    [TestMethod]
    public void SimulationTick_WhenRunning_ShouldKeepBallsInsidePlane()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlane(200, 150);
        logicApi.PlaceBalls(7, 12);
        logicApi.StartSimulation();

        for (int i = 0; i < 800; i++)
        {
            logicApi.SimulationTick();
        }

        foreach (Ball ball in dataApi.GetBalls())
        {
            Assert.IsTrue(ball.X - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.X + ball.Radius <= 200 + 1e-6);
            Assert.IsTrue(ball.Y - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.Y + ball.Radius <= 150 + 1e-6);
        }
    }
}

internal sealed class FakeDataApi : IDataApi
{
    private readonly List<Ball> balls = [];

    public Ball CreateBall(double x, double y, double radius)
    {
        Ball ball = new(x, y, radius);
        balls.Add(ball);
        return ball;
    }

    public IReadOnlyCollection<Ball> GetBalls() => balls.AsReadOnly();

    public void ClearBalls() => balls.Clear();

    public void UpdateBall(Ball ball, double x, double y)
    {
        if (!balls.Contains(ball))
        {
            throw new InvalidOperationException("Ball is not tracked by this fake repository.");
        }

        ball.SetPosition(x, y);
    }
}
