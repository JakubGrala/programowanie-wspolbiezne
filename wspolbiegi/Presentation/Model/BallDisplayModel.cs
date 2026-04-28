using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Presentation.Model;

public sealed class BallDisplayModel : INotifyPropertyChanged
{
    private double logicCenterX;
    private double logicCenterY;
    private double logicRadius;
    private double displayLeft;
    private double displayTop;
    private double displayDiameter;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double DisplayLeft
    {
        get => displayLeft;
        private set => SetField(ref displayLeft, value);
    }

    public double DisplayTop
    {
        get => displayTop;
        private set => SetField(ref displayTop, value);
    }

    public double DisplayDiameter
    {
        get => displayDiameter;
        private set => SetField(ref displayDiameter, value);
    }

    public void SetLogical(double centerX, double centerY, double radius)
    {
        logicCenterX = centerX;
        logicCenterY = centerY;
        logicRadius = radius;
    }

    public void RefreshDisplay(double scale, double offsetX, double offsetY)
    {
        DisplayLeft = ((logicCenterX - logicRadius) * scale) + offsetX;
        DisplayTop = ((logicCenterY - logicRadius) * scale) + offsetY;
        DisplayDiameter = logicRadius * 2 * scale;
    }

    private void SetField(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
