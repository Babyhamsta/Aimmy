﻿using Aimmy2.UILibrary;
using Class;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Aimmy2.Class;
using Aimmy2.Config;
using InputLogic;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using UILibrary;

namespace Aimmy2.Extensions;

public static class UIElementExtensions
{
    public static void MoveToScreenCenter(this Window window, System.Windows.Forms.Screen screen)
    {
        if (!window.CheckAccess())
        {
            window.Dispatcher.Invoke(() => window.MoveToScreenCenter(screen));
            return;
        }
        // Convert the window size to pixels, because WPF uses device-independent units
        double dpiX, dpiY;
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
        }
        else
        {
            dpiX = 96.0;
            dpiY = 96.0;
        }

        // Get the size of the window in pixels
        double windowWidth = window.Width * dpiX / 96.0;
        double windowHeight = window.Height * dpiY / 96.0;

        // Calculate the new top-left position to center the window on the given screen
        double newLeft = screen.WorkingArea.Left + (screen.WorkingArea.Width - windowWidth) / 2;
        double newTop = screen.WorkingArea.Top + (screen.WorkingArea.Height - windowHeight) / 2;

        // Move the window
        window.Left = newLeft;
        window.Top = newTop;
    }

    public static T InitWith<T>(this T component, Action<T>? cfg) where T : UIElement
    {
        if (cfg != null)
        {
            if(component.IsVisible)
            {
                cfg.Invoke(component);
            }
            else
            {
                DependencyPropertyChangedEventHandler visibleChangeHandler = null;
                visibleChangeHandler = (s, e) => component.Dispatcher.BeginInvoke(() =>
                {
                    component.IsVisibleChanged -= visibleChangeHandler;
                    cfg.Invoke(component);
                });
                component.IsVisibleChanged += visibleChangeHandler;
            }
        }

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



    public static void BindMouseGradientAngle(this Border sender, Func<bool>? condition = null)
    {

        LinearGradientBrush linearGradientBrush = null;

        if (sender.Background is LinearGradientBrush originalBrush)
        {
            linearGradientBrush = originalBrush.Clone();
            sender.Background = linearGradientBrush;
        }

        if (linearGradientBrush == null)
        {
            var resourceRotateTransform = sender.TryFindResource("RotaryGradient") as RotateTransform ??
                                          App.Current.TryFindResource("RotaryGradient") as RotateTransform;
            if (resourceRotateTransform != null)
            {
                var clonedRotateTransform = resourceRotateTransform.Clone();

                linearGradientBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0.5, 1),
                    RelativeTransform = new TransformGroup
                    {
                        Children = new TransformCollection
                        {
                            new ScaleTransform { CenterX = 0.5, CenterY = 0.5 },
                            new SkewTransform { CenterX = 0.5, CenterY = 0.5 },
                            clonedRotateTransform,
                            new TranslateTransform()
                        }
                    }
                };
                sender.Background = linearGradientBrush;
            }
        }

        if (linearGradientBrush?.RelativeTransform is TransformGroup newTransformGroup)
        {
            foreach (var transform in newTransformGroup.Children)
            {
                if (transform is RotateTransform rotateTransform)
                {
                    sender.BindMouseGradientAngle(rotateTransform, condition);
                    return;
                }
            }
        }

    }

    public static void BindMouseGradientAngle(this FrameworkElement sender, RotateTransform? transform, Func<bool>? condition)
    {
        if (transform == null)
            return;

        double currentGradientAngle = 0;
        sender.MouseMove += (s, e) =>
        {
            if (condition != null && !condition.Invoke())
            {
                transform.Angle = 0;
                return;
            }

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


    public static T RemoveAll<T>(this T panel) where T : Panel
    {
        panel.Children.Clear();
        return panel;
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

    internal static AToggle AddToggleWithKeyBind<T>(this T owner, string title, InputBindingManager bindingManager, Action<AToggle>? cfg = null, Action<Border>? containerCfg = null) where T : IAddChild, new()
    {
        var border = owner.Add<Border>(p =>
        {
            p.MinWidth = 220;
            p.Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60));
            p.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
            p.BorderThickness = new Thickness(1, 0, 1, 0);
        }).InitWith(containerCfg);
        var grid = border.Add<Grid>(p =>
        {
            p.HorizontalAlignment = HorizontalAlignment.Stretch;
        });
        var innergrid = grid.Add<Grid>(p => p.HorizontalAlignment = HorizontalAlignment.Right);
        var panel = innergrid.Add<StackPanel>(p => p.Orientation = Orientation.Horizontal);
        grid.Add<TextBlock>(t =>
        {
            t.Text = title;
            t.Foreground = Brushes.White;
            t.Margin = new Thickness(10, 0, 5, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Left;
        });
        var toggle = new AToggle("");
        toggle.Background = toggle.BorderBrush = Brushes.Transparent;
        var code = AKeyChanger.CodeFor(title);
        var keyCodeValue = AppConfig.Current.BindingSettings[code]?.ToString();
        bool updating = false;
        panel.AddKeyChanger(
            code,
            () => keyCodeValue ?? "None", bindingManager, changer =>
            {
                changer.Background = changer.BorderBrush = Brushes.Transparent;

                if (keyCodeValue != null)
                {
                    bindingManager.SetupDefault(code, keyCodeValue);
                }

                changer.ShowTitle = false;
                bindingManager.OnBindingPressed += (key) =>
                {
                    if (changer.HasKeySet && key == code && !updating)
                    {
                        updating = true;
                        toggle.ToggleState();
                        Task.Delay(300).ContinueWith(_ => updating = false);
                    }
                };
            } );
        panel.Add(toggle, cfg);
        return toggle;
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
        return panel.Add(new APButton(title), button =>
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
        var keyChanger = panel.Add(new AKeyChanger(title, keybind), keyChanger =>
        {
            cfg?.Invoke(keyChanger);
        });

        if (bindingManager == null)
        {
            return keyChanger;
        }

        keyChanger.KeyDeleted += (sender, e) =>
        {
            AppConfig.Current.BindingSettings[title] = "";
            keyChanger.SetContent("");
        };

        keyChanger.Reader.Click += (sender, e) =>
        {
            if(keyChanger.InUpdateMode)
                return;
            keyChanger.InUpdateMode = true;
            keyChanger.SetContent("...");
            keyChanger.ToolTip = "Press any key to set the binding";
            bindingManager.StartListeningForBinding(title);

            // Event handler for setting the binding
            Action<string, string>? bindingSetHandler = null;
            bindingSetHandler = (bindingId, key) =>
            {
                if (bindingId == title)
                {
                    keyChanger.SetContent(key);
                    keyChanger.ToolTip = string.Empty;
                    AppConfig.Current.BindingSettings[bindingId] = key;
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
            colorChanger.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(AppConfig.Current.ColorState[title].ToString());
        });
    }

    public static ASlider AddSlider(this IAddChild panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool forAntiRecoil = false)
    {
        return panel.Add<ASlider>(new ASlider(title, label, frequency), slider =>
        {
            slider.Slider.Minimum = min;
            slider.Slider.Maximum = max;
            slider.Slider.TickFrequency = frequency;
        });
    }

    public static ADropdown AddDropdown<T>(this IAddChild panel, string title, T value, IEnumerable<T> items, Action<T> onSelect, Action<ADropdown>? cfg = null)
    {
        var res = panel.Add<ADropdown>(new ADropdown(title), dropdown =>
        {
            cfg?.Invoke(dropdown);
        });
        foreach (var v in items)
        {
            res.AddDropdownItem(v.ToString(), item =>
            {
                if (v.Equals(value))
                {
                    res.DropdownBox.SelectedItem = item;
                }

                item.Selected += (s, e) => { onSelect(v); };
            });
        }

        return res;
    }

    public static ADropdown AddDropdown<TEnum>(this IAddChild panel, string title, TEnum value, Action<TEnum> onSelect, Action<ADropdown>? cfg = null) where TEnum : struct, Enum
    {
        var res = panel.Add<ADropdown>(new ADropdown(title), dropdown =>
        {
            cfg?.Invoke(dropdown);
        });
        Enum<TEnum>.GetValues().Apply(v => res.AddDropdownItem(v.ToDescriptionString(), item =>
        {
            if (v.Equals(value))
            {
                res.DropdownBox.SelectedItem = item;
            }
            item.Selected += (s, e) =>
            {
                onSelect(v);
            };
        }));

        return res;
    }


    public static ComboBoxItem AddDropdownItem(this ADropdown dropdown, string title, Action<ComboBoxItem>? cfg = null)
    {
        var fontName = "Atkinson Hyperlegible";
        var fontFamily = (dropdown.TryFindResource(fontName) ?? Application.Current.TryFindResource(fontName) ?? Application.Current.MainWindow?.TryFindResource(fontName)) as FontFamily;
        var dropdownItem = new ComboBoxItem
        {
            Content = title,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            FontFamily = fontFamily
        };
        
        cfg?.Invoke(dropdownItem);
        dropdown.DropdownBox.Items.Add(dropdownItem);
        return dropdownItem;
    }

    public static AFileLocator AddFileLocator(this IAddChild panel, string title, string filter = "All files (*.*)|*.*", string dlExtension = "", Action<AFileLocator>? cfg = null)
    {
        string path = title; // TODO: DIe sind doch alle dumm
        return panel.Add(new AFileLocator(title, path, filter, dlExtension), fileLocator =>
        {
            cfg?.Invoke(fileLocator);
        });
    }

    public static ATitle AddTitle(this IAddChild panel, string title, bool canMinimize = false, Action<ATitle>? cfg = null)
    {
        return panel.Add<ATitle>(new ATitle(title, canMinimize), atitle =>
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