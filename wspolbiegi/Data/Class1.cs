using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Data;

public interface IBall
{
    Guid Id { get; }

    double Radius { get; }

    double Mass { get; }
}

public interface IDiagnosticLogger : IDisposable
{
    void Log(BallSnapshot snapshot);
}

public readonly struct BallSnapshot
{
    public BallSnapshot(Guid id, IBall ball, double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        Id = id;
        Ball = ball;
        X = x;
        Y = y;
        Radius = radius;
        Mass = mass;
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public Guid Id { get; }

    public IBall Ball { get; }

    public double X { get; }

    public double Y { get; }

    public double Radius { get; }

    public double Mass { get; }

    public double VelocityX { get; }

    public double VelocityY { get; }
}

public class BallEventArgs : EventArgs
{
    public BallEventArgs(IBall ball)
    {
        Ball = ball;
    }

    public IBall Ball { get; }
}

internal sealed class Ball : IBall, IDisposable
{
    private readonly object sync = new();
    private double x;
    private double y;
    private double velocityX;
    private double velocityY;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? moveTask;
    private static readonly TimeSpan MovePeriod = TimeSpan.FromMilliseconds(20);

    public Ball(double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        }

        if (mass <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be greater than zero.");
        }

        Id = Guid.NewGuid();
        this.x = x;
        this.y = y;
        Radius = radius;
        Mass = mass;
        this.velocityX = velocityX;
        this.velocityY = velocityY;
    }

    public event EventHandler<BallEventArgs>? PositionChanged;

    public Guid Id { get; }

    public double Radius { get; }

    public double Mass { get; }

    public BallSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new BallSnapshot(Id, this, x, y, Radius, Mass, velocityX, velocityY);
        }
    }

    public void Advance(double deltaSeconds)
    {
        lock (sync)
        {
            x += velocityX * deltaSeconds;
            y += velocityY * deltaSeconds;
        }
    }

    public void MoveBy(double deltaX, double deltaY)
    {
        lock (sync)
        {
            x += deltaX;
            y += deltaY;
        }
    }

    public void SetPosition(double newX, double newY)
    {
        lock (sync)
        {
            x = newX;
            y = newY;
        }
    }

    public void SetVelocity(double newVelocityX, double newVelocityY)
    {
        lock (sync)
        {
            velocityX = newVelocityX;
            velocityY = newVelocityY;
        }
    }

    public void Start()
    {
        if (cancellationTokenSource != null)
        {
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        moveTask = Task.Run(() => MoveLoopAsync(cancellationTokenSource.Token));
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        moveTask?.Wait();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        moveTask = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task MoveLoopAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(MovePeriod);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Advance(MovePeriod.TotalSeconds);
                PositionChanged?.Invoke(this, new BallEventArgs(this));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

internal interface IBallRepository
{
    Ball Add(double x, double y, double radius, double mass, double velocityX, double velocityY);

    IReadOnlyList<Ball> GetAll();

    void Clear();
}

internal sealed class InMemoryBallRepository : IBallRepository
{
    private readonly object sync = new();
    private readonly List<Ball> balls = [];

    public Ball Add(double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        Ball ball = new(x, y, radius, mass, velocityX, velocityY);
        lock (sync)
        {
            balls.Add(ball);
        }

        return ball;
    }

    public IReadOnlyList<Ball> GetAll()
    {
        lock (sync)
        {
            return balls.ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            balls.Clear();
        }
    }
}

public interface IDataApi
{
    event EventHandler<BallEventArgs>? BallPositionChanged;

    IBall CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY);

    IReadOnlyList<BallSnapshot> GetSnapshots();

    void ClearBalls();

    void StartAll();

    void StopAll();

    void SetPosition(IBall ball, double x, double y);

    void MoveBy(IBall ball, double deltaX, double deltaY);

    void SetVelocity(IBall ball, double velocityX, double velocityY);
}

public sealed class DataApi : IDataApi, IDisposable
{
    private readonly IBallRepository repository;
    private readonly IDiagnosticLogger logger;

    public DataApi()
        : this(new InMemoryBallRepository(), new DataLogger("diagnostic.log"))
    {
    }

    public DataApi(IDiagnosticLogger logger)
        : this(new InMemoryBallRepository(), logger)
    {
    }

    internal DataApi(IBallRepository repository, IDiagnosticLogger logger)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<BallEventArgs>? BallPositionChanged;

    public IBall CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        Ball ball = repository.Add(x, y, radius, mass, velocityX, velocityY);
        ball.PositionChanged += OnBallPositionChanged;
        return ball;
    }

    public IReadOnlyList<BallSnapshot> GetSnapshots() =>
        repository.GetAll().Select(ball => ball.GetSnapshot()).ToList().AsReadOnly();

    public void ClearBalls()
    {
        StopAll();
        foreach (var ball in repository.GetAll())
        {
            ball.PositionChanged -= OnBallPositionChanged;
            ball.Dispose();
        }
        repository.Clear();
    }

    public void StartAll()
    {
        foreach (Ball ball in repository.GetAll())
        {
            ball.Start();
        }
    }

    public void StopAll()
    {
        foreach (Ball ball in repository.GetAll())
        {
            ball.Stop();
        }
    }

    public void SetPosition(IBall ball, double x, double y) => AsDataBall(ball).SetPosition(x, y);

    public void MoveBy(IBall ball, double deltaX, double deltaY) => AsDataBall(ball).MoveBy(deltaX, deltaY);

    public void SetVelocity(IBall ball, double velocityX, double velocityY) => AsDataBall(ball).SetVelocity(velocityX, velocityY);

    public void Dispose()
    {
        ClearBalls();
        logger.Dispose();
    }

    private static Ball AsDataBall(IBall ball) =>
        ball as Ball ?? throw new ArgumentException("Ball was not created by this Data layer.", nameof(ball));

    private void OnBallPositionChanged(object? sender, BallEventArgs e)
    {
        logger.Log(AsDataBall(e.Ball).GetSnapshot());
        BallPositionChanged?.Invoke(this, e);
    }
}

public sealed class DataLogger : IDiagnosticLogger
{
    private readonly BlockingCollection<string> queue = new();
    private readonly TextWriter writer;
    private readonly Task writeTask;
    private volatile bool disposed;

    public DataLogger(string filePath)
        : this(new StreamWriter(filePath, append: false, Encoding.ASCII))
    {
    }

    public DataLogger(TextWriter writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        writeTask = Task.Run(WriteLoop);
    }

    public void Log(BallSnapshot snapshot)
    {
        if (disposed) return;

        string logEntry = $@"{{
    ""Timestamp"": ""{DateTime.UtcNow:O}"",
    ""Id"": ""{snapshot.Id}"",
    ""X"": {snapshot.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},
    ""Y"": {snapshot.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)},
    ""VelocityX"": {snapshot.VelocityX.ToString(System.Globalization.CultureInfo.InvariantCulture)},
    ""VelocityY"": {snapshot.VelocityY.ToString(System.Globalization.CultureInfo.InvariantCulture)}
}}";

        try
        {
            queue.Add(logEntry);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void WriteLoop()
    {
        foreach (string message in queue.GetConsumingEnumerable())
        {
            writer.WriteLine(message);
        }

        writer.Flush();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        queue.CompleteAdding();
        writeTask.Wait();
        writer.Dispose();
        queue.Dispose();
    }
}
