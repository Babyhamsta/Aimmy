using Aimmy2.Class;
using AimmyWPF.Class;
using Class;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aimmy2.Extensions;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for ConfigSaver.xaml
    /// </summary>
    public partial class ConfigSaver 
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
            MainBorder.BindMouseGradientAngle(RotaryGradient, ShouldBindGradientMouse);
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
        
        #endregion Window Controls
    }
}