using AimmyWPF.Class;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : System.Windows.Controls.UserControl
    {
        private static Color EnableColor = (Color)ColorConverter.ConvertFromString("#FF722ED1");
        private static Color DisableColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
        private static TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);

        public AToggle(string Text)
        {
            InitializeComponent();
            ToggleTitle.Content = Text;
        }

        public void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        public void EnableSwitch()
        {
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, EnableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, -1, 0));
        }

        public void DisableSwitch()
        {
            Color currentColor = (Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty);
            SetColorAnimation(currentColor, DisableColor, AnimationDuration);
            Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, 16, 0));
        }
    }
}