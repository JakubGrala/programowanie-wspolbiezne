using System.Windows;
using Presentation.ViewModel;

namespace Presentation.Attached;

public static class PlayfieldSizeObserver
{
    public static readonly DependencyProperty ObserveProperty = DependencyProperty.RegisterAttached(
        "Observe",
        typeof(bool),
        typeof(PlayfieldSizeObserver),
        new PropertyMetadata(false, OnObserveChanged));

    public static void SetObserve(FrameworkElement element, bool value) => element.SetValue(ObserveProperty, value);

    public static bool GetObserve(FrameworkElement element) => (bool)element.GetValue(ObserveProperty);

    private static void OnObserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            fe.SizeChanged += OnSizeChanged;
            fe.Loaded += OnLoaded;
        }
        else
        {
            fe.SizeChanged -= OnSizeChanged;
            fe.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Notify(sender);

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Notify(sender);

    private static void Notify(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is MainViewModel viewModel)
        {
            viewModel.OnPlayfieldHostSizeChanged(fe.ActualWidth, fe.ActualHeight);
        }
    }
}
