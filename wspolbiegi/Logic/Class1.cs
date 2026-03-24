using System;
using System.Collections.Generic;
using System.Linq;
using Data;

namespace Logic;

public sealed class LogicBall
{
    public LogicBall(double x, double y, double radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public double X { get; }

    public double Y { get; }

    public double Radius { get; }
}

public interface ILogicApi
{
    void CreatePlane(double width, double height);

    IReadOnlyCollection<LogicBall> PlaceBalls(int count, double radius);

    IReadOnlyCollection<LogicBall> GetBalls();
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
    private readonly IDataApi dataApi;
    private readonly IRandomProvider randomProvider;
    private Plane? currentPlane;

    public LogicApi(IDataApi dataApi, IRandomProvider randomProvider)
    {
        this.dataApi = dataApi ?? throw new ArgumentNullException(nameof(dataApi));
        this.randomProvider = randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
    }

    public void CreatePlane(double width, double height)
    {
        currentPlane = new Plane(width, height);
        dataApi.ClearBalls();
    }

    public IReadOnlyCollection<LogicBall> PlaceBalls(int count, double radius)
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
            dataApi.CreateBall(x, y, radius);
        }

        return GetBalls();
    }

    public IReadOnlyCollection<LogicBall> GetBalls() =>
        dataApi.GetBalls()
            .Select(ball => new LogicBall(ball.X, ball.Y, ball.Radius))
            .ToList()
            .AsReadOnly();

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
