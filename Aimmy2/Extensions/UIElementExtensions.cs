using Aimmy2.UILibrary;
using Class;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Aimmy2.Class;
using InputLogic;
using UILibrary;

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


    public static T Add<T>(this IAddChild panel, Func<T> ctor, Action<T>? cfg = null) where T : UIElement => panel.Add<T>(ctor(), cfg);

    public static T Add<T>(this IAddChild panel, Action<T>? cfg = null) where T : UIElement, new() => panel.Add(new T(), cfg);

    public static T Add<T>(this IAddChild panel, T element, Action<T>? cfg = null) where T : UIElement
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            panel.AddChild(element);
        });
        if (cfg != null)
            element.InitWith(cfg);
        return element;
    }

    public static AToggle AddToggle(this IAddChild panel, string title, Action<AToggle>? cfg = null)
    {
        return panel.Add<AToggle>(toggle =>
        {
            toggle.Text = title;
            cfg?.Invoke(toggle);
        });
    }

    public static APButton AddButton(this IAddChild panel, string title, Action<APButton>? cfg = null)
    {
        return panel.Add(new APButton(title),button =>
        {
            cfg?.Invoke(button);
        });
    }

    // Similarly, add methods for other UI elements...
    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, Func<string> keybind,
        InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null)
    {
        return panel.AddKeyChanger(title, keybind(), bindingManager, cfg);
    }
    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, string keybind, InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null)
    {
        var keyChanger = panel.Add(new AKeyChanger(title, keybind),keyChanger =>
        {
            cfg?.Invoke(keyChanger);
        });

        if (bindingManager == null)
        {
            return keyChanger;
        }

        keyChanger.KeyDeleted += (sender, e) =>
        {
            Dictionary.bindingSettings[title] = "";
            keyChanger.SetContent("");
        };

        keyChanger.Reader.Click += (sender, e) =>
        {
            keyChanger.InUpdateMode = true;
            keyChanger.SetContent("...");
            bindingManager.StartListeningForBinding(title);

            // Event handler for setting the binding
            Action<string, string>? bindingSetHandler = null;
            bindingSetHandler = (bindingId, key) =>
            {
                if (bindingId == title)
                {
                    keyChanger.SetContent(key);
                    Dictionary.bindingSettings[bindingId] = key;
                    bindingManager.OnBindingSet -= bindingSetHandler; // Unsubscribe after setting
                    Task.Delay(300).ContinueWith(_ => keyChanger.InUpdateMode = false);
                }
            };

            bindingManager.OnBindingSet += bindingSetHandler;
        };
        return keyChanger;
    }

    public static AColorChanger AddColorChanger(this IAddChild panel, string title)
    {
        return panel.Add<AColorChanger>(new AColorChanger(title), colorChanger =>
        {
            colorChanger.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState[title]);
        });
    }

    public static ASlider AddSlider(this IAddChild panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool forAntiRecoil = false)
    {
        return panel.Add<ASlider>(new ASlider(title, label, frequency) ,slider =>
        {
            slider.Slider.Minimum = min;
            slider.Slider.Maximum = max;
            slider.Slider.TickFrequency = frequency;

            var settings = forAntiRecoil ? Dictionary.AntiRecoilSettings : Dictionary.sliderSettings;
            slider.Slider.Value = settings.TryGetValue(title, out var value) ? value : min;

            slider.Slider.ValueChanged += (s, e) => settings[title] = slider.Slider.Value;
        });
    }

    public static ADropdown AddDropdown(this IAddChild panel, string title, Action<ADropdown>? cfg = null)
    {
        string path = title; // TODO: DIe sind doch alle dumm
        return panel.Add<ADropdown>(new ADropdown(title, path), dropdown =>
        {
            cfg?.Invoke(dropdown);
        });
    }

    public static ComboBoxItem AddDropdownItem(this ADropdown dropdown, string title, Action<ComboBoxItem>? cfg = null)
    {
        // TODO: Check if tryfindresource is working
        var dropdownItem = new ComboBoxItem
        {
            Content = title,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            FontFamily = dropdown.TryFindResource("Atkinson Hyperlegible") as FontFamily
        };

        dropdownItem.Selected += (s, e) =>
        {
            string? key = dropdown.DropdownTitle.Content?.ToString();
            if (key != null) Dictionary.dropdownState[key] = title;
            else throw new NullReferenceException("dropdown.DropdownTitle.Content.ToString() is null");
        };

        cfg?.Invoke(dropdownItem);
        dropdown.DropdownBox.Items.Add(dropdownItem);
        return dropdownItem;
    }

    public static AFileLocator AddFileLocator(this IAddChild panel, string title, string filter = "All files (*.*)|*.*", string dlExtension = "", Action<AFileLocator>? cfg = null)
    {
        string path = title; // TODO: DIe sind doch alle dumm
        return panel.Add(new AFileLocator(title, path, filter, dlExtension) ,fileLocator =>
        {
            cfg?.Invoke(fileLocator);
        });
    }

    public static ATitle AddTitle(this IAddChild panel, string title, bool canMinimize = false, Action<ATitle>? cfg = null)
    {
        return panel.Add<ATitle>(new ATitle(title, canMinimize) ,atitle =>
        {
            cfg?.Invoke(atitle);
        });
    }

    public static void AddSeparator(this IAddChild panel)
    {
        panel.Add<ARectangleBottom>();
        panel.Add<ASpacer>();
    }

    public static ACredit AddCredit(this IAddChild panel, string name, string role, Action<ACredit>? cfg = null)
    {
        return panel.Add(new ACredit(name, role), credit =>
        {
            cfg?.Invoke(credit);
        });
    }

}