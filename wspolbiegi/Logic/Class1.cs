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
    private const double SimulationDeltaSeconds = 1.0 / 60.0;
    private const int SimulationDelayMilliseconds = 16;

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
            ResolveBallCollisions(e.Ball);
            
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

        dataApi.ClearBalls();

        for (int i = 0; i < count; i++)
        {
            double x = minX + ((maxX - minX) * randomProvider.NextDouble());
            double y = minY + ((maxY - minY) * randomProvider.NextDouble());
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

    private void ResolveWallCollisions(Plane plane, Ball movingBall)
    {
        BallSnapshot snapshot = dataApi.GetSnapshots().First(s => s.Ball == movingBall);
        
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
            dataApi.SetPosition(snapshot.Ball, x, y);
        }
        if (velocityChanged)
        {
            dataApi.SetVelocity(snapshot.Ball, velocityX, velocityY);
        }
    }

    private void ResolveBallCollisions(Ball movingBall)
    {
        IReadOnlyList<BallSnapshot> snapshots = dataApi.GetSnapshots();
        if (snapshots.Count < 2)
        {
            return;
        }

        BallSnapshot first = snapshots.First(s => s.Ball == movingBall);

        foreach (BallSnapshot second in snapshots)
        {
            if (first.Ball == second.Ball)
            {
                continue;
            }

            ResolveBallPair(first.Ball, second.Ball);
            
            first = dataApi.GetSnapshots().First(s => s.Ball == movingBall);
        }
    }

    private void ResolveBallPair(Ball firstBall, Ball secondBall)
    {
        BallSnapshot first = dataApi.GetSnapshots().First(snapshot => snapshot.Ball == firstBall);
        BallSnapshot second = dataApi.GetSnapshots().First(snapshot => snapshot.Ball == secondBall);

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

        dataApi.SetPosition(first.Ball, first.X - correctionX, first.Y - correctionY);
        dataApi.SetPosition(second.Ball, second.X + correctionX, second.Y + correctionY);

        double relativeVelocityX = second.VelocityX - first.VelocityX;
        double relativeVelocityY = second.VelocityY - first.VelocityY;
        double velocityAlongNormal = (relativeVelocityX * normalX) + (relativeVelocityY * normalY);

        if (velocityAlongNormal > 0)
        {
            return;
        }

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

internal sealed class BallCollisionBinaryTree
{
    private readonly Node? root;

    private BallCollisionBinaryTree(Node? root)
    {
        this.root = root;
    }

    public static BallCollisionBinaryTree Build(IReadOnlyList<BallSnapshot> snapshots)
    {
        List<BallSnapshot> sorted = snapshots.OrderBy(snapshot => snapshot.X).ToList();
        Node? rootNode = BuildNode(sorted, 0, sorted.Count - 1);
        return new BallCollisionBinaryTree(rootNode);
    }

    public IEnumerable<BallSnapshot> FindCandidates(BallSnapshot target, double pairDistance)
    {
        if (root is null)
        {
            yield break;
        }

        foreach (BallSnapshot candidate in Search(root, target.X - pairDistance, target.X + pairDistance))
        {
            if (candidate.Ball != target.Ball)
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<BallSnapshot> Search(Node? node, double minX, double maxX)
    {
        if (node is null)
        {
            yield break;
        }

        if (node.Snapshot.X < minX)
        {
            foreach (BallSnapshot snapshot in Search(node.Right, minX, maxX))
            {
                yield return snapshot;
            }

            yield break;
        }

        if (node.Snapshot.X > maxX)
        {
            foreach (BallSnapshot snapshot in Search(node.Left, minX, maxX))
            {
                yield return snapshot;
            }

            yield break;
        }

        yield return node.Snapshot;

        foreach (BallSnapshot left in Search(node.Left, minX, maxX))
        {
            yield return left;
        }

        foreach (BallSnapshot right in Search(node.Right, minX, maxX))
        {
            yield return right;
        }
    }

    private static Node? BuildNode(List<BallSnapshot> sorted, int leftIndex, int rightIndex)
    {
        if (leftIndex > rightIndex)
        {
            return null;
        }

        int middle = (leftIndex + rightIndex) / 2;
        return new Node(
            sorted[middle],
            BuildNode(sorted, leftIndex, middle - 1),
            BuildNode(sorted, middle + 1, rightIndex));
    }

    private sealed class Node
    {
        public Node(BallSnapshot snapshot, Node? left, Node? right)
        {
            Snapshot = snapshot;
            Left = left;
            Right = right;
        }

        public BallSnapshot Snapshot { get; }

        public Node? Left { get; }

        public Node? Right { get; }
    }
}
