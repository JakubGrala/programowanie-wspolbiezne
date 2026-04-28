using System;
using System.Collections.Generic;
using System.Linq;
using Data;

namespace Logic;

public readonly struct Velocity2D
{
    public Velocity2D(double deltaX, double deltaY)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public double DeltaX { get; }

    public double DeltaY { get; }

    public double Magnitude => Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
}

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

    void StartSimulation();

    void StopSimulation();

    bool IsSimulationRunning { get; }

    void SimulationTick();
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
    private const double MaxStepPerTick = 18.0;

    private readonly IDataApi dataApi;
    private readonly IRandomProvider randomProvider;
    private Plane? currentPlane;
    private bool simulationRunning;

    public LogicApi(IDataApi dataApi, IRandomProvider randomProvider)
    {
        this.dataApi = dataApi ?? throw new ArgumentNullException(nameof(dataApi));
        this.randomProvider = randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
    }

    public bool IsSimulationRunning => simulationRunning;

    public void CreatePlane(double width, double height)
    {
        currentPlane = new Plane(width, height);
        dataApi.ClearBalls();
        simulationRunning = false;
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

    public void StartSimulation()
    {
        _ = currentPlane ?? throw new InvalidOperationException("Create plane before starting simulation.");

        if (!dataApi.GetBalls().Any())
        {
            throw new InvalidOperationException("Place balls before starting simulation.");
        }

        simulationRunning = true;
    }

    public void StopSimulation() => simulationRunning = false;

    public void SimulationTick()
    {
        if (!simulationRunning)
        {
            return;
        }

        Plane plane = currentPlane ?? throw new InvalidOperationException("Plane is not initialized.");

        foreach (Ball ball in dataApi.GetBalls())
        {
            Velocity2D step = CreateRandomStep();
            double candidateX = ball.X + step.DeltaX;
            double candidateY = ball.Y + step.DeltaY;

            double clampedX = Clamp(candidateX, ball.Radius, plane.Width - ball.Radius);
            double clampedY = Clamp(candidateY, ball.Radius, plane.Height - ball.Radius);

            dataApi.UpdateBall(ball, clampedX, clampedY);
        }
    }

    private Velocity2D CreateRandomStep()
    {
        double angle = randomProvider.NextDouble() * Math.PI * 2;
        double length = randomProvider.NextDouble() * MaxStepPerTick;
        double deltaX = Math.Cos(angle) * length;
        double deltaY = Math.Sin(angle) * length;
        return new Velocity2D(deltaX, deltaY);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
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
