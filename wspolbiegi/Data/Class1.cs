using System;
using System.Collections.Generic;
using System.Linq;

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

public sealed class Ball
{
    private readonly object sync = new();
    private double x;
    private double y;
    private double velocityX;
    private double velocityY;

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
    Ball CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY);

    IReadOnlyList<BallSnapshot> GetSnapshots();

    void ClearBalls();

    void AdvanceAll(double deltaSeconds);

    void SetPosition(Ball ball, double x, double y);

    void SetVelocity(Ball ball, double velocityX, double velocityY);
}

public sealed class DataApi : IDataApi
{
    private readonly IBallRepository repository;

    public DataApi(IBallRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Ball CreateBall(double x, double y, double radius, double mass, double velocityX, double velocityY) =>
        repository.Add(x, y, radius, mass, velocityX, velocityY);

    public IReadOnlyList<BallSnapshot> GetSnapshots() =>
        repository.GetAll().Select(ball => ball.GetSnapshot()).ToList().AsReadOnly();

    public void ClearBalls() => repository.Clear();

    public void AdvanceAll(double deltaSeconds)
    {
        foreach (Ball ball in repository.GetAll())
        {
            ball.Advance(deltaSeconds);
        }
    }

    public void SetPosition(Ball ball, double x, double y) => ball.SetPosition(x, y);

    public void SetVelocity(Ball ball, double velocityX, double velocityY) => ball.SetVelocity(velocityX, velocityY);
}
