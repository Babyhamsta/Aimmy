using AimmyAimbot.Class;
using AimmyAimbot.UserController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AimmyAimbot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        BrushConverter brushcolor = new BrushConverter();

        #region Window Position Values
        Thickness WinTooLeft = new Thickness(-1680, 0, 1680, 0);
        Thickness WinVeryLeft = new Thickness(-1120, 0, 1120, 0);
        Thickness WinLeft = new Thickness(-560, 0, 560, 0);

        Thickness WinCenter = new Thickness(0, 0, 0, 0);

        Thickness WinRight = new Thickness(560, 0, -560, 0);
        Thickness WinVeryRight = new Thickness(1120, 0, -1120, 0);
        Thickness WinTooRight = new Thickness(1680, 0, -1680, 0);
        #endregion

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            #region Set Margins

            AimMenu.Margin = WinCenter;
            TriggerMenu.Margin = WinRight;
            SelectorMenu.Margin = WinRight;
            SettingsMenu.Margin = WinTooRight;

            #endregion

            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
        }

        #region Menu Controls

        // shit code ik

        private async void Selection1_Click(object sender, RoutedEventArgs e)
        {
            Selection1.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");
            Selection2.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection3.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection4.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, new Thickness(10, 30, 414, 0));

            EnableAllWindows();

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinCenter);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinRight);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinVeryRight);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinTooRight);

            await Task.Delay(500);

            AimMenu.Visibility = Visibility.Visible;
            TriggerMenu.Visibility = Visibility.Collapsed;
            SelectorMenu.Visibility = Visibility.Collapsed;
            SettingsMenu.Visibility = Visibility.Collapsed;
        }

        private async void Selection2_Click(object sender, RoutedEventArgs e)
        {
            Selection1.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection2.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");
            Selection3.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection4.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, new Thickness(144, 30, 278, 0));

            EnableAllWindows();

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinCenter);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinRight);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinVeryRight);

            await Task.Delay(500);

            AimMenu.Visibility = Visibility.Collapsed;
            TriggerMenu.Visibility = Visibility.Visible;
            SelectorMenu.Visibility = Visibility.Collapsed;
            SettingsMenu.Visibility = Visibility.Collapsed;
        }

        private async void Selection3_Click(object sender, RoutedEventArgs e)
        {
            Selection1.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection2.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection3.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");
            Selection4.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, new Thickness(280, 30, 144, 0));

            EnableAllWindows();

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinVeryLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinCenter);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinRight);

            await Task.Delay(500);

            AimMenu.Visibility = Visibility.Collapsed;
            TriggerMenu.Visibility = Visibility.Collapsed;
            SelectorMenu.Visibility = Visibility.Visible;
            SettingsMenu.Visibility = Visibility.Collapsed;
        }

        private async void Selection4_Click(object sender, RoutedEventArgs e)
        {
            Selection1.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection2.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection3.Foreground = (Brush)brushcolor.ConvertFromString("#ffffff");
            Selection4.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, new Thickness(414, 30, 10, 0));

            EnableAllWindows();

            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinTooLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinVeryLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinLeft);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinCenter);

            await Task.Delay(500);

            AimMenu.Visibility = Visibility.Collapsed;
            TriggerMenu.Visibility = Visibility.Collapsed;
            SelectorMenu.Visibility = Visibility.Collapsed;
            SettingsMenu.Visibility = Visibility.Visible;
        }

        #endregion

        #region Aim Menu
        void LoadAimMenu()
        {
            #region Enable AI Aim Aligner
            AToggle Enable_AIAimAligner = new AToggle("Enable AI Aim Aligner"); // Title

            // Set Defaults / Saved Settings here
            Enable_AIAimAligner.DisableSwitch();

            // END HERE

            Enable_AIAimAligner.Reader.Click += (s, x) =>
            {
                // Insert Toggle Functionality Here
                switch (Bools.AIAimAligner)
                {
                    case true:
                        Bools.AIAimAligner = false;
                        Enable_AIAimAligner.DisableSwitch();
                        break;
                    case false:
                        Bools.AIAimAligner = true;
                        Enable_AIAimAligner.EnableSwitch();
                        break;
                }
            };
            AimScroller.Children.Add(Enable_AIAimAligner);
            #endregion

            #region Enable Third Person Aim
            AToggle ThirdPersonAim = new AToggle("Enable Third Person Aim"); // Title

            // Set Defaults / Saved Settings here
            ThirdPersonAim.DisableSwitch();

            // END HERE

            ThirdPersonAim.Reader.Click += (s, x) =>
            {
                switch (Bools.ThirdPersonAim)
                {
                    // Insert Toggle Functionality Here
                    case true:
                        Bools.ThirdPersonAim = false;
                        ThirdPersonAim.DisableSwitch();
                        break;
                    case false:
                        Bools.ThirdPersonAim = true;
                        ThirdPersonAim.EnableSwitch();
                        break;
                }
            };
            AimScroller.Children.Add(ThirdPersonAim);
            #endregion

            #region Change KeyPress
            AKeyChanger Change_KeyPress = new AKeyChanger("Change KeyPress", "Right Click");  // Title

            Change_KeyPress.Reader.Click += (s, x) =>
            {
                // Insert Button Functionality Here
            };
            AimScroller.Children.Add(Change_KeyPress);
            #endregion

            #region Pixel Increase (Sensitivity)
            ASlider PixelSensitivity = new ASlider("Pixel Sensitivty", "Sensitivty"); // Title

            // Set Defaults / Saved Settings here
            PixelSensitivity.Slider.Minimum = 1;
            PixelSensitivity.Slider.Maximum = 10;
            PixelSensitivity.Slider.Value = 2;
            PixelSensitivity.Slider.TickFrequency = 0.01;

            // END HERE

            PixelSensitivity.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(PixelSensitivity);
            #endregion

            #region In-Game X & Y Sensitivity
            ASlider XYSensitivity = new ASlider("In-Game X & Y Sensitivity", "Sensitivity"); // Title

            // Set Defaults / Saved Settings here
            XYSensitivity.Slider.Minimum = 1;
            XYSensitivity.Slider.Maximum = 1000;
            XYSensitivity.Slider.Value = 100;
            XYSensitivity.Slider.TickFrequency = 1;

            // END HERE

            XYSensitivity.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(XYSensitivity);
            #endregion

            #region Head Offset (Y Axis)
            ASlider HeadOffset = new ASlider("Head Offset (Y Axis)", "Sensitivity"); // Title

            // Set Defaults / Saved Settings here
            HeadOffset.Slider.Minimum = 1;
            HeadOffset.Slider.Maximum = 10;
            HeadOffset.Slider.Value = 4;
            HeadOffset.Slider.TickFrequency = 0.01;

            HeadOffset.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(HeadOffset);
            #endregion
        }
        #endregion
        #region Trigger Menu
        void LoadTriggerMenu()
        {
            #region Enable TriggerBot
            AToggle Enable_TriggerBot = new AToggle("Enable TriggerBot"); // Title

            // Set Defaults / Saved Settings here
            Enable_TriggerBot.DisableSwitch();

            // END HERE

            Enable_TriggerBot.Reader.Click += (s, x) =>
            {
                // Insert Toggle Functionality Here
                switch (Bools.Triggerbot)
                {
                    case true:
                        Bools.Triggerbot = false;
                        Enable_TriggerBot.DisableSwitch();
                        break;
                    case false:
                        Bools.Triggerbot = true;
                        Enable_TriggerBot.EnableSwitch();
                        break;
                }
            };
            TriggerScroller.Children.Add(Enable_TriggerBot);
            #endregion

            #region TriggerBot Delay
            ASlider TriggerBot_Delay = new ASlider("TriggerBot Delay", "milliseconds"); // Title

            // Set Defaults / Saved Settings here
            TriggerBot_Delay.Slider.Minimum = 1;
            TriggerBot_Delay.Slider.Maximum = 1000;
            TriggerBot_Delay.Slider.Value = 100;
            TriggerBot_Delay.Slider.TickFrequency = 1;

            TriggerBot_Delay.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            TriggerScroller.Children.Add(TriggerBot_Delay);
            #endregion
        }
        #endregion
        #region Selection Menu

        /*
         * The Selection Menu contains a ListBox for selecting the model,
         * a Label to highlight the selected model, and a Button to open the folder.
         *
         *ListBox: SelectorListBox
         *Label: SelectedModelNotifier
         *Button: OpenModelFolder
         *
         *Do what you can with these.
         */

        private void OpenModelFolder_Click(object sender, RoutedEventArgs e)
        {

        }

        #endregion
        #region Settings Menu
        void LoadSettingsMenu()
        {
            #region Collect Data While Playing
            AToggle CollectDataWhilePlaying = new AToggle("Collect Data While Playing"); // Title

            // Set Defaults / Saved Settings here
            CollectDataWhilePlaying.DisableSwitch();

            // END HERE

            CollectDataWhilePlaying.Reader.Click += (s, x) =>
            {
                // Insert Toggle Functionality Here
                switch (Bools.CollectDataWhilePlaying)
                {
                    case true:
                        Bools.CollectDataWhilePlaying = false;
                        CollectDataWhilePlaying.DisableSwitch();
                        break;
                    case false:
                        Bools.CollectDataWhilePlaying = true;
                        CollectDataWhilePlaying.EnableSwitch();
                        break;
                }
            };
            SettingsScroller.Children.Add(CollectDataWhilePlaying);
            #endregion

            #region AI Minimum Confidence
            ASlider AIMinimumConfidence = new ASlider("AI Minimum Confidence", "% Confidence"); // Title

            // Set Defaults / Saved Settings here
            AIMinimumConfidence.Slider.Minimum = 1;
            AIMinimumConfidence.Slider.Maximum = 100;
            AIMinimumConfidence.Slider.Value = 80;
            AIMinimumConfidence.Slider.TickFrequency = 1;

            // END HERE

            AIMinimumConfidence.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };
            #endregion

            #region Change KeyPress
            AButton ClearSettings = new AButton("Clear Settings");  // Title

            ClearSettings.Reader.Click += (s, x) =>
            {
                // Insert Button Functionality Here
            };
            SettingsScroller.Children.Add(ClearSettings);
            #endregion
        }
        #endregion

        void EnableAllWindows()
        {
            AimMenu.Visibility = Visibility.Visible;
            TriggerMenu.Visibility = Visibility.Visible;
            SelectorMenu.Visibility = Visibility.Visible;
            SettingsMenu.Visibility = Visibility.Visible;
        }

    }
}
