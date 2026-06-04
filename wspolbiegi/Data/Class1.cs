using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Data;

public readonly struct BallSnapshot
{
    public BallSnapshot(Ball ball, double x, double y, double radius, double mass, double velocityX, double velocityY)
    {
        Ball = ball;
        X = x;
        Y = y;
        Radius = radius;
        Mass = mass;
        VelocityX = velocityX;
        VelocityY = velocityY;
    }

    public Ball Ball { get; }

    public double X { get; }

    public double Y { get; }

    public double Radius { get; }

    public double Mass { get; }

    public double VelocityX { get; }

    public double VelocityY { get; }
}

public class BallEventArgs : EventArgs
{
    public BallEventArgs(Ball ball)
    {
        Ball = ball;
    }

    public Ball Ball { get; }
}

public sealed class Ball : IDisposable
{
    private readonly object sync = new();
    private double x;
    private double y;
    private double velocityX;
    private double velocityY;
    private CancellationTokenSource? cancellationTokenSource;
    private Task? moveTask;

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

        this.x = x;
        this.y = y;
        Radius = radius;
        Mass = mass;
        this.velocityX = velocityX;
        this.velocityY = velocityY;
    }

    public event EventHandler<BallEventArgs>? PositionChanged;

    public double Radius { get; }

    public double Mass { get; }

    public BallSnapshot GetSnapshot()
    {
        lock (sync)
        {
            return new BallSnapshot(this, x, y, Radius, Mass, velocityX, velocityY);
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
        const double deltaSeconds = 0.015;
        while (!cancellationToken.IsCancellationRequested)
        {
            Advance(deltaSeconds);
            PositionChanged?.Invoke(this, new BallEventArgs(this));

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(deltaSeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}

public interface IBallRepository
{
    Ball Add(double x, double y, double radius, double mass, double velocityX, double velocityY);

    IReadOnlyList<Ball> GetAll();

    void Clear();
}

public sealed class InMemoryBallRepository : IBallRepository
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

    Ball CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY);

    IReadOnlyList<BallSnapshot> GetSnapshots();

    void ClearBalls();

    void StartAll();

    void StopAll();

    void SetPosition(Ball ball, double x, double y);

    void SetVelocity(Ball ball, double velocityX, double velocityY);
}

public sealed class DataApi : IDataApi, IDisposable
{
    private readonly IBallRepository repository;

    public DataApi(IBallRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public event EventHandler<BallEventArgs>? BallPositionChanged;

    public Ball CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY)
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

    public void SetPosition(Ball ball, double x, double y) => ball.SetPosition(x, y);

    public void SetVelocity(Ball ball, double velocityX, double velocityY) => ball.SetVelocity(velocityX, velocityY);

    public void Dispose()
    {
        ClearBalls();
    }

    private void OnBallPositionChanged(object? sender, BallEventArgs e)
    {
        BallPositionChanged?.Invoke(this, e);
    }
}
