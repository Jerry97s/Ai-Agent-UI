using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiAgentUi.Views.Behaviors;

public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static readonly DependencyProperty WheelStepProperty =
        DependencyProperty.RegisterAttached(
            "WheelStep",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(48d));

    public static void SetWheelStep(DependencyObject element, double value) => element.SetValue(WheelStepProperty, value);
    public static double GetWheelStep(DependencyObject element) => (double)element.GetValue(WheelStepProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.Control c)
            return;

        if ((bool)e.NewValue)
            c.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            c.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject d)
            return;

        var viewer = FindScrollViewer(d);
        if (viewer is null)
            return;

        var step = GetWheelStep(d);
        var direction = e.Delta > 0 ? -1 : 1;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset + direction * step);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv)
            return sv;

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }
        return null;
    }
}

