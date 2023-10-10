using AimmyAimbot.Class;
using AimmyAimbot.UserController;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AimmyAimbot
{
    public partial class MainWindow : Window
    {
        private readonly BrushConverter brushcolor = new BrushConverter();

        private enum MenuPosition
        {
            AimMenu,
            TriggerMenu,
            SelectorMenu,
            SettingsMenu
        }

        Thickness WinTooLeft = new Thickness(-1680, 0, 1680, 0);
        Thickness WinVeryLeft = new Thickness(-1120, 0, 1120, 0);
        Thickness WinLeft = new Thickness(-560, 0, 560, 0);

        Thickness WinCenter = new Thickness(0, 0, 0, 0);

        Thickness WinRight = new Thickness(560, 0, -560, 0);
        Thickness WinVeryRight = new Thickness(1120, 0, -1120, 0);
        Thickness WinTooRight = new Thickness(1680, 0, -1680, 0);

        public MainWindow()
        {
            InitializeComponent();

            #region Set Margins

            AimMenu.Margin = WinCenter;
            TriggerMenu.Margin = WinRight;
            SelectorMenu.Margin = WinRight;
            SettingsMenu.Margin = WinTooRight;

            #endregion

            InitializeMenuPositions();
            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
        }

        #region Menu Initialization and Setup

        private void InitializeMenuPositions()
        {
            AimMenu.Margin = new Thickness(0, 0, 0, 0);
            TriggerMenu.Margin = new Thickness(560, 0, -560, 0);
            SelectorMenu.Margin = new Thickness(560, 0, -560, 0);
            SettingsMenu.Margin = new Thickness(1680, 0, -1680, 0);
        }

        private void SetupToggle(AToggle toggle, Action<bool> action, bool initialState)
        {
            toggle.Reader.Tag = initialState;
            SetToggleState(toggle);

            toggle.Reader.Click += (s, x) =>
            {
                bool currentState = (bool)toggle.Reader.Tag;
                toggle.Reader.Tag = !currentState;
                SetToggleState(toggle);
                action.Invoke(!currentState);
            };
        }

        private void SetToggleState(AToggle toggle)
        {
            bool state = (bool)toggle.Reader.Tag;

            if (state)
            {
                toggle.EnableSwitch();
            }
            else
            {
                toggle.DisableSwitch();
            }
        }

        #endregion

        #region Menu Controls

        private async void Selection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                MenuPosition position = (MenuPosition)Enum.Parse(typeof(MenuPosition), clickedButton.Tag.ToString());
                ResetMenuColors();
                clickedButton.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");
                ApplyMenuAnimations(position);
                UpdateMenuVisibility(position);
            }
        }

        private void ResetMenuColors()
        {
            Selection1.Foreground = Selection2.Foreground = Selection3.Foreground = Selection4.Foreground =
                (Brush)brushcolor.ConvertFromString("#ffffff");
        }

        private void ApplyMenuAnimations(MenuPosition position)
        {
            Thickness highlighterMargin = new Thickness(0, 30, 414, 0);
            switch (position)
            {
                case MenuPosition.AimMenu:
                    highlighterMargin = new Thickness(10, 30, 414, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinVeryRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinTooRight);
                    break;

                case MenuPosition.TriggerMenu:
                    highlighterMargin = new Thickness(144, 30, 278, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinVeryRight);
                    break;

                case MenuPosition.SelectorMenu:
                    highlighterMargin = new Thickness(280, 30, 144, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinRight);
                    break;

                case MenuPosition.SettingsMenu:
                    highlighterMargin = new Thickness(414, 30, 10, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinTooLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinCenter);
                    break;
            }
        }

        private void UpdateMenuVisibility(MenuPosition position)
        {
            AimMenu.Visibility = (position == MenuPosition.AimMenu) ? Visibility.Visible : Visibility.Collapsed;
            TriggerMenu.Visibility = (position == MenuPosition.TriggerMenu) ? Visibility.Visible : Visibility.Collapsed;
            SelectorMenu.Visibility = (position == MenuPosition.SelectorMenu) ? Visibility.Visible : Visibility.Collapsed;
            SettingsMenu.Visibility = (position == MenuPosition.SettingsMenu) ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion


        void LoadAimMenu()
        {
            AToggle Enable_AIAimAligner = new AToggle("Enable AI Aim Aligner");
            SetupToggle(Enable_AIAimAligner, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Enable_AIAimAligner);

            AToggle ThirdPersonAim = new AToggle("Enable Third Person Aim");
            SetupToggle(ThirdPersonAim, state => Bools.ThirdPersonAim = state, Bools.ThirdPersonAim);
            AimScroller.Children.Add(ThirdPersonAim);

            AKeyChanger Change_KeyPress = new AKeyChanger("Change KeyPress", "Right Click");
            Change_KeyPress.Reader.Click += (s, x) =>
            {
                // Insert Button Functionality Here
            };
            AimScroller.Children.Add(Change_KeyPress);

            ASlider PixelSensitivity = new ASlider("Pixel Sensitivty", "Sensitivty");

            PixelSensitivity.Slider.Minimum = 1;
            PixelSensitivity.Slider.Maximum = 10;
            PixelSensitivity.Slider.Value = 2;
            PixelSensitivity.Slider.TickFrequency = 0.01;
            PixelSensitivity.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(PixelSensitivity);

            ASlider XYSensitivity = new ASlider("In-Game X & Y Sensitivity", "Sensitivity");

            XYSensitivity.Slider.Minimum = 1;
            XYSensitivity.Slider.Maximum = 1000;
            XYSensitivity.Slider.Value = 100;
            XYSensitivity.Slider.TickFrequency = 1;
            XYSensitivity.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(XYSensitivity);

            ASlider HeadOffset = new ASlider("Head Offset (Y Axis)", "Sensitivity");

            HeadOffset.Slider.Minimum = 1;
            HeadOffset.Slider.Maximum = 10;
            HeadOffset.Slider.Value = 4;
            HeadOffset.Slider.TickFrequency = 0.01;
            HeadOffset.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            AimScroller.Children.Add(HeadOffset);
        }

        void LoadTriggerMenu()
        {
            AToggle Enable_TriggerBot = new AToggle("Enable TriggerBot");
            SetupToggle(Enable_TriggerBot, state => Bools.Triggerbot = state, Bools.Triggerbot);
            TriggerScroller.Children.Add(Enable_TriggerBot);

            ASlider TriggerBot_Delay = new ASlider("TriggerBot Delay", "milliseconds");

            TriggerBot_Delay.Slider.Minimum = 1;
            TriggerBot_Delay.Slider.Maximum = 1000;
            TriggerBot_Delay.Slider.Value = 100;
            TriggerBot_Delay.Slider.TickFrequency = 1;
            TriggerBot_Delay.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            TriggerScroller.Children.Add(TriggerBot_Delay);

        }

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

        void LoadSettingsMenu()
        {
            AToggle CollectDataWhilePlaying = new AToggle("Collect Data While Playing");
            SetupToggle(CollectDataWhilePlaying, state => Bools.CollectDataWhilePlaying = state, Bools.CollectDataWhilePlaying);
            SettingsScroller.Children.Add(CollectDataWhilePlaying);

            ASlider AIMinimumConfidence = new ASlider("AI Minimum Confidence", "% Confidence");

            AIMinimumConfidence.Slider.Minimum = 1;
            AIMinimumConfidence.Slider.Maximum = 100;
            AIMinimumConfidence.Slider.Value = 80;
            AIMinimumConfidence.Slider.TickFrequency = 1;
            AIMinimumConfidence.Slider.ValueChanged += (s, x) =>
            {
                // Insert Slider Functionality Here
            };

            SettingsScroller.Children.Add(AIMinimumConfidence);

            AButton ClearSettings = new AButton("Clear Settings");

            ClearSettings.Reader.Click += (s, x) =>
            {
                // Insert Button Functionality Here
            };

            SettingsScroller.Children.Add(ClearSettings);
        }


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
    }
}
