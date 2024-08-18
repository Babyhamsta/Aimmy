﻿using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AimmyWPF.Class;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aimmy2.Types;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Reflection;
using System.Windows.Controls;
using Aimmy2.Config;
using Aimmy2.Extensions;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : INotifyPropertyChanged
    {
        private static Color EnableColor => ApplicationConstants.AccentColor;
        private static Color DisableColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
        private static TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);

        public event EventHandler<EventArgs> Activated;
        public event EventHandler<EventArgs> Deactivated;
        public event EventHandler<EventArgs<bool>> Changed;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (value)
                    {
                        EnableSwitch();
                    }
                    else
                    {
                        DisableSwitch();
                    }
                }
            }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(AToggle), new PropertyMetadata("Aim only when Trigger is set"));

        private bool _checked;


        public AToggle()
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C"));
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF"));
            InitializeComponent();
            DataContext = this;
            ApplicationConstants.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ApplicationConstants.AccentColor) && _checked)
                {
                    Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
                    SetColorAnimation(currentColor, EnableColor, AnimationDuration);
                }
            };
        }


        public AToggle BindTo(Expression<Func<bool>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Checked = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Checked = fn.Compile()();
                }
            };

            Changed += (s, e) =>
            {
                propertyInfo.SetValue(owner, e.Value);
            };

            return this;
        }

        public AToggle(string text) : this()
        {
            Text = text;
        }

        public void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        public bool ToggleState()
        {
            if (Checked)
            {
                DisableSwitch();
            }
            else
            {
                EnableSwitch();
            }

            return Checked;
        }

        public void EnableSwitch()
        {
            _checked = true;
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, EnableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, -1, 0));
            Activated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new EventArgs<bool>(true));
            OnPropertyChanged(nameof(Checked));
        }

        public void DisableSwitch()
        {
            _checked = false;
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, DisableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, 16, 0));
            Deactivated?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, new EventArgs<bool>(false));
            OnPropertyChanged(nameof(Checked));
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleState();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void BindActiveStateColor(StackPanel panel)
        {
            void SetColor(bool isChecked)
            {
                var title = panel.FindChildren<ATitle>().FirstOrDefault();
                var themeForActive = ThemePalette.ThemeForActive;
                if (title != null)
                {
                    title.LabelTitle.Foreground = isChecked ? new SolidColorBrush(themeForActive.AccentColor) : Brushes.White;
                }

                var solidColorBrush = new SolidColorBrush(themeForActive.MainColor)
                {
                    Opacity = AppConfig.Current.ToggleState.GlobalActive ? 1 : 0.35
                };
                panel.Background = isChecked ? solidColorBrush : Brushes.Transparent;
            }
            SetColor(Checked);
            Changed += (s, e) => SetColor(e.Value);
            AppConfig.Current.ToggleState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppConfig.Current.ToggleState.GlobalActive))
                {
                    SetColor(Checked);
                }
            };
        }
    }
}