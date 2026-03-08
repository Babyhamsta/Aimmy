using Aimmy2.Theme;
using AimmyWPF.Class;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : System.Windows.Controls.UserControl
    {
        private static readonly Color DisableColor = Colors.White;
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);
        private bool _isEnabled = false;

        public bool IsSwitchedOn => _isEnabled;

        public AToggle(string Text, string? tooltip = null)
        {
            InitializeComponent();
            ToggleTitle.Content = Text;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is Style style)
                    tt.Style = style;
                ToolTip = tt;
            }

            // Subscribe to theme change events
            ThemeManager.ThemeChanged += OnThemeChanged;

            // Update theme on load
            this.Loaded += (s, e) => RefreshThemeColors();

            // Cleanup on unload
            this.Unloaded += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, Color newThemeColor)
        {
            Application.Current.Dispatcher.BeginInvoke(() => RefreshThemeColors());
        }

        private void RefreshThemeColors()
        {
            // Update border
            SwitchBorder.BorderBrush = new SolidColorBrush(ThemeManager.ThemeColor);

            // Update toggle dot if enabled
            if (_isEnabled)
            {
                SwitchMoving.Background = new SolidColorBrush(ThemeManager.ThemeColor);
            }
        }

        private Color GetCurrentColor()
        {
            return SwitchMoving.Background is SolidColorBrush brush ? brush.Color : DisableColor;
        }

        public void EnableSwitch()
        {
            _isEnabled = true;
            Color themeColor = ThemeManager.ThemeColor;

            SetColorAnimation(GetCurrentColor(), themeColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, -1, 0));
        }

        public void DisableSwitch()
        {
            _isEnabled = false;

            SetColorAnimation(GetCurrentColor(), DisableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, 16, 0));
        }

        private void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background = new SolidColorBrush(fromColor);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
    }
}