using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data;

namespace Logic;

public readonly struct Velocity2D
{
    public Velocity2D(double velocityX, double velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public double VelocityX { get; }

    public double VelocityY { get; }

    public double Magnitude => Math.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
}

public sealed class LogicBall
{
    public LogicBall(
        double x,
        double y,
        double radius,
        double mass,
        double velocityX,
        double velocityY)
    {
        X = x;
        Y = y;
        Radius = radius;
        Mass = mass;
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public double X { get; }

    public double Y { get; }

    public double Radius { get; }

    public double Mass { get; }

    public double VelocityX { get; }

    public double VelocityY { get; }
}

public sealed class BallsUpdatedEventArgs : EventArgs
{
    public BallsUpdatedEventArgs(IReadOnlyList<LogicBall> balls)
    {
        Balls = balls;
    }

    public IReadOnlyList<LogicBall> Balls { get; }
}

public interface ILogicApi
{
    event EventHandler<BallsUpdatedEventArgs>? BallsUpdated;

    Task CreatePlaneAsync(double width, double height);

    Task<IReadOnlyCollection<LogicBall>> PlaceBallsAsync(int count, double radius);

    Task StartSimulationAsync();

    Task StopSimulationAsync();

    bool IsSimulationRunning { get; }
}

public interface IRandomProvider
{
    double NextDouble();
}

public sealed class DefaultRandomProvider : IRandomProvider
{
    private readonly Random random = new(42);

    public double NextDouble() => random.NextDouble();
}

public sealed class LogicApi : ILogicApi
{
    private const double InitialSpeed = 180.0;
    private const int MaxPlacementAttempts = 1000;

    private readonly IDataApi dataApi;
    private readonly IRandomProvider randomProvider;
    private readonly object integrationCriticalSection = new();

    private Plane? currentPlane;

    private volatile bool simulationRunning;

    public LogicApi(IDataApi dataApi, IRandomProvider randomProvider)
    {
        this.dataApi = dataApi ?? throw new ArgumentNullException(nameof(dataApi));
        this.randomProvider = randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
        this.dataApi.BallPositionChanged += OnBallPositionChanged;
    }

    public event EventHandler<BallsUpdatedEventArgs>? BallsUpdated;

    public bool IsSimulationRunning => simulationRunning;

    public Task CreatePlaneAsync(double width, double height) =>
        Task.Run(() =>
        {
            lock (integrationCriticalSection)
            {
                currentPlane = new Plane(width, height);
                dataApi.ClearBalls();
                simulationRunning = false;
            }
        });

    public Task<IReadOnlyCollection<LogicBall>> PlaceBallsAsync(int count, double radius) =>
        Task.Run(() =>
        {
            lock (integrationCriticalSection)
            {
                return PlaceBalls(count, radius);
            }
        });

    public async Task StartSimulationAsync()
    {
        lock (integrationCriticalSection)
        {
            _ = currentPlane ?? throw new InvalidOperationException("Create plane before starting simulation.");

            if (!dataApi.GetSnapshots().Any())
            {
                throw new InvalidOperationException("Place balls before starting simulation.");
            }

            if (simulationRunning)
            {
                return;
            }

            simulationRunning = true;
            dataApi.StartAll();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopSimulationAsync()
    {
        lock (integrationCriticalSection)
        {
            simulationRunning = false;
        }
        dataApi.StopAll();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void OnBallPositionChanged(object? sender, BallEventArgs e)
    {
        IReadOnlyList<LogicBall> balls;
        lock (integrationCriticalSection)
        {
            Plane plane = currentPlane ?? throw new InvalidOperationException("Plane is not initialized.");

            ResolveWallCollisions(plane, e.Ball);
            ResolveBallCollisions(plane, e.Ball);
            
            balls = BuildLogicBalls();
        }

        BallsUpdated?.Invoke(this, new BallsUpdatedEventArgs(balls));
    }

    private IReadOnlyCollection<LogicBall> PlaceBalls(int count, double radius)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Ball count must be greater than zero.");
        }

        Plane plane = currentPlane ?? throw new InvalidOperationException("Create plane before placing balls.");

        double minX = radius;
        double maxX = plane.Width - radius;
        double minY = radius;
        double maxY = plane.Height - radius;

        if (minX > maxX || minY > maxY)
        {
            throw new InvalidOperationException("Plane is too small for the requested ball radius.");
        }

        List<(double X, double Y)> positions = new List<(double X, double Y)>();
        double minDistanceSquared = (2 * radius) * (2 * radius);

        for (int i = 0; i < count; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < MaxPlacementAttempts && !placed; attempt++)
            {
                double x = minX + ((maxX - minX) * randomProvider.NextDouble());
                double y = minY + ((maxY - minY) * randomProvider.NextDouble());

                bool overlaps = positions.Any(p => ((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)) < minDistanceSquared);
                if (!overlaps)
                {
                    positions.Add((x, y));
                    placed = true;
                }
            }

            if (!placed)
            {
                break;
            }
        }

        dataApi.ClearBalls();

        foreach ((double x, double y) in positions)
        {
            double mass = radius * radius * (0.85 + (0.3 * randomProvider.NextDouble()));
            Velocity2D velocity = CreateRandomVelocity();
            dataApi.CreateBall(x, y, radius, mass, velocity.VelocityX, velocity.VelocityY);
        }

        return BuildLogicBalls();
    }

    private Velocity2D CreateRandomVelocity()
    {
        double angle = randomProvider.NextDouble() * Math.PI * 2;
        double velocityX = Math.Cos(angle) * InitialSpeed;
        double velocityY = Math.Sin(angle) * InitialSpeed;
        return new Velocity2D(velocityX, velocityY);
    }

    private IReadOnlyList<LogicBall> BuildLogicBalls() =>
        dataApi.GetSnapshots()
            .Select(snapshot => new LogicBall(
                snapshot.X,
                snapshot.Y,
                snapshot.Radius,
                snapshot.Mass,
                snapshot.VelocityX,
                snapshot.VelocityY))
            .ToList()
            .AsReadOnly();

    private void ResolveWallCollisions(Plane plane, IBall movingBall)
    {
        BallSnapshot snapshot = dataApi.GetSnapshot(movingBall);
        
        double x = snapshot.X;
        double y = snapshot.Y;
        double velocityX = snapshot.VelocityX;
        double velocityY = snapshot.VelocityY;
        double radius = snapshot.Radius;

        bool positionChanged = false;
        bool velocityChanged = false;

        if (x - radius < 0)
        {
            x = radius;
            velocityX = Math.Abs(velocityX);
            positionChanged = true;
            velocityChanged = true;
        }
        else if (x + radius > plane.Width)
        {
            x = plane.Width - radius;
            velocityX = -Math.Abs(velocityX);
            positionChanged = true;
            velocityChanged = true;
        }

        if (y - radius < 0)
        {
            y = radius;
            velocityY = Math.Abs(velocityY);
            positionChanged = true;
            velocityChanged = true;
        }
        else if (y + radius > plane.Height)
        {
            y = plane.Height - radius;
            velocityY = -Math.Abs(velocityY);
            positionChanged = true;
            velocityChanged = true;
        }

        if (positionChanged)
        {
            dataApi.MoveBy(snapshot.Ball, x - snapshot.X, y - snapshot.Y);
        }
        if (velocityChanged)
        {
            dataApi.SetVelocity(snapshot.Ball, velocityX, velocityY);
        }
    }

    private void ResolveBallCollisions(Plane plane, IBall movingBall)
    {
        IReadOnlyList<BallSnapshot> snapshots = dataApi.GetSnapshots();

        foreach (BallSnapshot other in snapshots)
        {
            if (other.Ball == movingBall)
            {
                continue;
            }

            ResolveBallPair(plane, movingBall, other.Ball);
        }
    }

    private void ResolveBallPair(Plane plane, IBall firstBall, IBall secondBall)
    {
        BallSnapshot first = dataApi.GetSnapshot(firstBall);
        BallSnapshot second = dataApi.GetSnapshot(secondBall);

        double deltaX = second.X - first.X;
        double deltaY = second.Y - first.Y;
        double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        double minDistance = first.Radius + second.Radius;

        if (distance <= 0.0001)
        {
            deltaX = 1;
            deltaY = 0;
            distance = 1;
        }

        if (distance >= minDistance)
        {
            return;
        }

        double normalX = deltaX / distance;
        double normalY = deltaY / distance;

        double overlap = minDistance - distance;
        double correctionX = normalX * (overlap / 2);
        double correctionY = normalY * (overlap / 2);

        dataApi.MoveBy(first.Ball, -correctionX, -correctionY);
        dataApi.MoveBy(second.Ball, correctionX, correctionY);

        double relativeVelocityX = second.VelocityX - first.VelocityX;
        double relativeVelocityY = second.VelocityY - first.VelocityY;
        double velocityAlongNormal = (relativeVelocityX * normalX) + (relativeVelocityY * normalY);

        if (velocityAlongNormal <= 0)
        {
            double impulseScalar = -(2 * velocityAlongNormal) / ((1 / first.Mass) + (1 / second.Mass));
            double impulseX = impulseScalar * normalX;
            double impulseY = impulseScalar * normalY;

            double firstVelocityX = first.VelocityX - (impulseX / first.Mass);
            double firstVelocityY = first.VelocityY - (impulseY / first.Mass);
            double secondVelocityX = second.VelocityX + (impulseX / second.Mass);
            double secondVelocityY = second.VelocityY + (impulseY / second.Mass);

            dataApi.SetVelocity(first.Ball, firstVelocityX, firstVelocityY);
            dataApi.SetVelocity(second.Ball, secondVelocityX, secondVelocityY);
        }

        ResolveWallCollisions(plane, firstBall);
        ResolveWallCollisions(plane, secondBall);
    }

    private sealed class Plane
    {
        public Plane(double width, double height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Plane width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Plane height must be greater than zero.");
            }

            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }
    }
}
