using Aimmy2.Class;
using AimmyWPF.Class;
using Class;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for ConfigSaver.xaml
    /// </summary>
    public partial class ConfigSaver : Window
    {
        private static Color EnableColor = (Color)ColorConverter.ConvertFromString("#FF722ED1");
        private static Color DisableColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
        private static TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);

        public void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new ColorAnimation(fromColor, toColor, duration);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private string ExtraStrings = string.Empty;

        public ConfigSaver()
        {
            InitializeComponent();
        }

        private void WriteJSON()
        {
            SaveDictionary.WriteJSON(Dictionary.sliderSettings, $"bin\\configs\\{ConfigNameTextbox.Text}.cfg", RecommendedModelNameTextBox.Text, ExtraStrings);
            new NoticeBar("Config has been saved to bin/configs.", 4000).Show();
            Close();
        }

        private void DownloadableModelChecker_Click(object sender, RoutedEventArgs e)
        {
            if (ExtraStrings == string.Empty)
            {
                ExtraStrings = " (Found in Downloadable Model menu)";
                SetColorAnimation((Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty), EnableColor, AnimationDuration);
                Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, -1, 0));
            }
            else
            {
                ExtraStrings = "";
                SetColorAnimation((Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty), DisableColor, AnimationDuration);
                Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new Thickness(0, 0, 16, 0));
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists($"bin/configs/{ConfigNameTextbox.Text}.cfg") ||
                MessageBox.Show("A config already exists with the same name, would you like to overwrite it?",
                    $"{Title} - Configuration Saver", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                WriteJSON();
            }
        }

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private double currentGradientAngle = 0;

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var CurrentMousePos = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(CurrentMousePos.X, CurrentMousePos.Y));
                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
                if (angleDifference > 180)
                {
                    angleDifference -= 360;
                }

                angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        #endregion Window Controls
    }
}