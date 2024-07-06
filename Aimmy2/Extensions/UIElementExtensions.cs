using Class;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Aimmy2.Extensions;

public static class UIElementExtensions
{
    public static T InitWith<T>(this T component, Action<T> cfg) where T : UIElement
    {
        DependencyPropertyChangedEventHandler visibleChangeHandler = null;
        visibleChangeHandler = (s, e) => component.Dispatcher.BeginInvoke(() =>
        {
            component.IsVisibleChanged -= visibleChangeHandler;
            cfg.Invoke(component);
        });
        component.IsVisibleChanged += visibleChangeHandler;
        return component;
    }

    public static T[] FindParents<T>(this UIElement element, Func<T, bool>? predicate = null) where T : UIElement
    {
        predicate ??= _ => true;
        var parents = new List<T>();

        DependencyObject current = element;
        while (current != null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T parent && predicate(parent))
            {
                parents.Add(parent);
            }
        }

        return parents.ToArray();
    }

    public static T[] FindChildren<T>(this UIElement element, Func<T, bool>? predicate = null) where T : UIElement
    {
        predicate ??= _ => true;
        var children = new List<T>();

        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T typedChild && predicate(typedChild))
            {
                children.Add(typedChild);
            }

            if (child is UIElement uiElement)
            {
                children.AddRange(uiElement.FindChildren(predicate));
            }
        }

        return children.ToArray();
    }


    public static void BindMouseGradientAngle(this FrameworkElement sender, RotateTransform transform, bool condition = true)
    {
        if (!condition)
            return;
        
        double currentGradientAngle = 0;
        sender.MouseMove += (s, e) =>
        {
            var currentMousePos = WinAPICaller.GetCursorPosition();
            var translatedMousePos = sender.PointFromScreen(new Point(currentMousePos.X, currentMousePos.Y));
            double targetAngle = Math.Atan2(translatedMousePos.Y - (sender.ActualHeight * 0.5), translatedMousePos.X - (sender.ActualWidth * 0.5)) * (180 / Math.PI);

            double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
            if (angleDifference > 180)
            {
                angleDifference -= 360;
            }

            angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
            currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
            transform.Angle = currentGradientAngle;
        };
    }
}