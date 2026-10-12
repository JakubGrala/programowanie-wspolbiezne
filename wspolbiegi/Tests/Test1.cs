using System.Linq;
using Data;
using Logic;

namespace Tests;

[TestClass]
public sealed class DataApiTests
{
    [TestMethod]
    public void CreateBall_ShouldStoreBallWithExpectedValues()
    {
        IDataApi api = new DataApi();

        IBall created = api.CreateBall(10, 20, 5, 2, 3, 4);

        BallSnapshot snapshot = api.GetSnapshots().Single(s => s.Ball == created);
        Assert.AreEqual(10, snapshot.X);
        Assert.AreEqual(20, snapshot.Y);
        Assert.AreEqual(5, snapshot.Radius);
        Assert.AreEqual(2, snapshot.Mass);
        Assert.AreEqual(3, snapshot.VelocityX);
        Assert.AreEqual(4, snapshot.VelocityY);
        Assert.AreEqual(1, api.GetSnapshots().Count);
    }

    [TestMethod]
    public void CreateBall_WithInvalidRadius_ShouldThrow()
    {
        IDataApi api = new DataApi();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => api.CreateBall(0, 0, 0, 1, 0, 0));
    }

    [TestMethod]
    public void GetSnapshots_ConcurrentReads_ShouldNotThrow()
    {
        IDataApi api = new DataApi();
        api.CreateBall(10, 10, 8, 1, 5, 5);
        api.CreateBall(30, 30, 8, 1, -4, 2);

        Parallel.For(0, 200, _ =>
        {
            _ = api.GetSnapshots().Count;
        });
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

        logicApi.CreatePlaneAsync(100, 80).GetAwaiter().GetResult();
        IReadOnlyCollection<LogicBall> balls = logicApi.PlaceBallsAsync(5, 10).GetAwaiter().GetResult();

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

        Assert.ThrowsException<InvalidOperationException>(() =>
            logicApi.PlaceBallsAsync(2, 5).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void StartSimulation_WithoutBalls_ShouldThrow()
    {
        IDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlaneAsync(100, 80).GetAwaiter().GetResult();

        Assert.ThrowsException<InvalidOperationException>(() =>
            logicApi.StartSimulationAsync().GetAwaiter().GetResult());
    }

    [TestMethod]
    public void CollisionDetection_ShouldKeepBallsInsidePlane()
    {
        FakeDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlaneAsync(200, 150).GetAwaiter().GetResult();
        logicApi.PlaceBallsAsync(7, 12).GetAwaiter().GetResult();

        for (int i = 0; i < 800; i++)
        {
            foreach (var ball in dataApi.GetRawBalls())
            {
                ball.Advance(1.0 / 60.0);
                dataApi.RaisePositionChanged(ball);
            }
        }

        foreach (BallSnapshot ball in dataApi.GetSnapshots())
        {
            Assert.IsTrue(ball.X - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.X + ball.Radius <= 200 + 1e-6);
            Assert.IsTrue(ball.Y - ball.Radius >= -1e-6);
            Assert.IsTrue(ball.Y + ball.Radius <= 150 + 1e-6);
        }
    }

    [TestMethod]
    public void ElasticCollision_ShouldExchangeMomentumAlongNormal()
    {
        FakeDataApi dataApi = new FakeDataApi();
        ILogicApi logicApi = new LogicApi(dataApi, new DefaultRandomProvider());

        logicApi.CreatePlaneAsync(400, 200).GetAwaiter().GetResult();
        dataApi.CreateBall(100, 100, 10, 1, 100, 0);
        dataApi.CreateBall(120, 100, 10, 1, -100, 0);

        for (int i = 0; i < 5; i++)
        {
            foreach (var ball in dataApi.GetRawBalls())
            {
                ball.Advance(1.0 / 120.0);
                dataApi.RaisePositionChanged(ball);
            }
        }

        BallSnapshot left = dataApi.GetSnapshots().OrderBy(snapshot => snapshot.X).First();
        BallSnapshot right = dataApi.GetSnapshots().OrderBy(snapshot => snapshot.X).Last();

        Assert.IsTrue(left.VelocityX < 0);
        Assert.IsTrue(right.VelocityX > 0);
    }
}

internal sealed class FakeDataApi : IDataApi
{
    private readonly List<FakeBall> balls = [];

    public event System.EventHandler<BallEventArgs>? BallPositionChanged;

    public void RaisePositionChanged(IBall ball) => BallPositionChanged?.Invoke(this, new BallEventArgs(ball));

    public IReadOnlyList<FakeBall> GetRawBalls() => balls.AsReadOnly();

    public IBall CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        FakeBall ball = new(x, y, radius, mass, velocityX, velocityY);
        balls.Add(ball);
        return ball;
    }

    public IReadOnlyList<BallSnapshot> GetSnapshots() =>
        balls.Select(ball => new BallSnapshot(ball, ball.X, ball.Y, ball.Radius, ball.Mass, ball.VelocityX, ball.VelocityY))
            .ToList()
            .AsReadOnly();

    public void ClearBalls() => balls.Clear();

    public void StartAll() { }

    public void StopAll() { }

    public void SetPosition(IBall ball, double x, double y)
    {
        FakeBall fake = (FakeBall)ball;
        fake.X = x;
        fake.Y = y;
    }

    public void SetVelocity(IBall ball, double velocityX, double velocityY)
    {
        FakeBall fake = (FakeBall)ball;
        fake.VelocityX = velocityX;
        fake.VelocityY = velocityY;
    }
}

internal sealed class FakeBall : IBall
{
    public FakeBall(double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        X = x;
        Y = y;
        Radius = radius;
        Mass = mass;
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public double Radius { get; }

    public double Mass { get; }

    public double X { get; set; }

    public double Y { get; set; }

    public double VelocityX { get; set; }

    public double VelocityY { get; set; }

    public void Advance(double deltaSeconds)
    {
        X += VelocityX * deltaSeconds;
        Y += VelocityY * deltaSeconds;
    }
}
