using System;
using System.Collections.Generic;
using System.Linq;

namespace Data;

public sealed class Ball
{
    public Ball(double x, double y, double radius)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        }

        X = x;
        Y = y;
        Radius = radius;
    }

    public double X { get; private set; }

    public double Y { get; private set; }

    public double Radius { get; }

    public void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public interface IBallRepository
{
    Ball Add(double x, double y, double radius);

    IReadOnlyCollection<Ball> GetAll();

    void Clear();

    void UpdatePosition(Ball ball, double x, double y);
}

public sealed class InMemoryBallRepository : IBallRepository
{
    private readonly List<Ball> balls = [];

    public Ball Add(double x, double y, double radius)
    {
        Ball ball = new(x, y, radius);
        balls.Add(ball);
        return ball;
    }

    public IReadOnlyCollection<Ball> GetAll() => balls.AsReadOnly();

    public void Clear() => balls.Clear();

    public void UpdatePosition(Ball ball, double x, double y)
    {
        if (!balls.Contains(ball))
        {
            throw new InvalidOperationException("Ball is not tracked by this repository.");
        }

        ball.SetPosition(x, y);
    }
}

public interface IDataApi
{
    Ball CreateBall(double x, double y, double radius);

    IReadOnlyCollection<Ball> GetBalls();

    void ClearBalls();

    void UpdateBall(Ball ball, double x, double y);
}

public sealed class DataApi : IDataApi
{
    private readonly IBallRepository repository;

    public DataApi(IBallRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Ball CreateBall(double x, double y, double radius) => repository.Add(x, y, radius);

    public IReadOnlyCollection<Ball> GetBalls() => repository.GetAll();

    public void ClearBalls() => repository.Clear();

    public void UpdateBall(Ball ball, double x, double y) => repository.UpdatePosition(ball, x, y);
}
