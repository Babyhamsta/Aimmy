using System.ComponentModel;
using System.Windows;
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
}