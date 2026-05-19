using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Presentation.Model;

public sealed class BallDisplayModel : INotifyPropertyChanged
{
    private double logicCenterX;
    private double logicCenterY;
    private double logicRadius;
    private double displayLeft;
    private double displayTop;
    private double displayDiameter;
    private Brush fill = Brushes.CornflowerBlue;

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

    public Brush Fill
    {
        get => fill;
        private set => SetField(ref fill, value);
    }

    public void SetLogical(double centerX, double centerY, double radius, double mass)
    {
        logicCenterX = centerX;
        logicCenterY = centerY;
        logicRadius = radius;
        Fill = CreateFillColor(mass);
    }

    public void RefreshDisplay(double scale, double offsetX, double offsetY)
    {
        DisplayLeft = ((logicCenterX - logicRadius) * scale) + offsetX;
        DisplayTop = ((logicCenterY - logicRadius) * scale) + offsetY;
        DisplayDiameter = logicRadius * 2 * scale;
    }

    private static Brush CreateFillColor(double mass)
    {
        double normalized = Math.Clamp(mass / 500.0, 0.2, 1.0);
        byte red = (byte)(70 + (normalized * 120));
        byte green = (byte)(110 + (normalized * 80));
        byte blue = (byte)(200 - (normalized * 60));
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
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

    private void SetField(ref Brush field, Brush value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
