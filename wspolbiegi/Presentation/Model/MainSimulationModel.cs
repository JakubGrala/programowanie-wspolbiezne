using System.Collections.ObjectModel;
using Logic;

namespace Presentation.Model;

/// <summary>
/// GUI model: maps logical coordinates from the logic layer to scaled pixel coordinates for the view.
/// </summary>
public sealed class MainSimulationModel
{
    private readonly double logicalPlaneWidth;
    private readonly double logicalPlaneHeight;

    public MainSimulationModel(double logicalPlaneWidth, double logicalPlaneHeight)
    {
        this.logicalPlaneWidth = logicalPlaneWidth;
        this.logicalPlaneHeight = logicalPlaneHeight;
    }

    public ObservableCollection<BallDisplayModel> Balls { get; } = [];

    public double LogicalPlaneWidth => logicalPlaneWidth;

    public double LogicalPlaneHeight => logicalPlaneHeight;

    public void SyncFromLogic(IReadOnlyCollection<LogicBall> logicBalls, double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        while (Balls.Count > logicBalls.Count)
        {
            Balls.RemoveAt(Balls.Count - 1);
        }

        int index = 0;
        foreach (LogicBall ball in logicBalls)
        {
            if (index >= Balls.Count)
            {
                Balls.Add(new BallDisplayModel());
            }

            Balls[index].SetLogical(ball.X, ball.Y, ball.Radius);
            index++;
        }

        double scaleX = viewportWidth / logicalPlaneWidth;
        double scaleY = viewportHeight / logicalPlaneHeight;
        double scale = Math.Min(scaleX, scaleY);
        double usedWidth = logicalPlaneWidth * scale;
        double usedHeight = logicalPlaneHeight * scale;
        double offsetX = (viewportWidth - usedWidth) / 2;
        double offsetY = (viewportHeight - usedHeight) / 2;

        foreach (BallDisplayModel ball in Balls)
        {
            ball.RefreshDisplay(scale, offsetX, offsetY);
        }
    }
}
