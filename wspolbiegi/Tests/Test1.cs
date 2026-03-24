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
}
