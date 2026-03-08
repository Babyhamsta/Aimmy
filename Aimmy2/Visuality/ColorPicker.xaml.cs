using Aimmy2.Theme;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace UISections
{
    public partial class ColorPicker : Window
    {
        //--
        public Color SelectedColor { get; private set; }
        public event Action<Color>? ColorChanged;
        //--
        private Color ThemeGradientColor => ThemeManager.ThemeColorDark;
        private double currentGradientAngle = 0;
        //==
        public string ColorPickerTitle { get; set; } = "Theme Color";
        //--
        public ColorPicker(Color initialColor, string title = "Theme Color")
        {
            InitializeComponent();
            //--
            ColorPickerTitle = title;
            ColorWheelControl.Title = ColorPickerTitle;
            ColorWheelControl.SuppressThemeApply = true;
            //--

            //Every .xaml with a border named "MainBorder" gets changed as long as this is visible, so double check! - Yes i copied and pasted this comment, i couldn't find anything better to say, ugh.
            ThemeManager.TrackWindow(this);

            ThemeManager.RegisterElement(this);
            ThemeManager.RegisterElement(ColorWheelControl);
            ColorWheelControl.SuppressThemeApply = true;
            ThemeManager.ThemeChanged += OnThemeChanged;
            ColorWheelControl.SetInitialColor(initialColor);
            ColorWheelControl.MouseLeftButtonUp += ColorWheelControl_MouseLeftButtonUp;
            ColorWheelControl.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    SelectedColor = ColorWheelControl.GetCurrentPreviewColor();
                    ColorChanged?.Invoke(SelectedColor);
                }
            };

            ColorWheelControl.BrightnessSlider.ValueChanged += (s, e) =>
            {
                SelectedColor = ColorWheelControl.GetCurrentPreviewColor();
                ColorChanged?.Invoke(SelectedColor);
            };

            UpdateThemeColors();
        }


        private void OnThemeChanged(object? sender, Color newColor)
        {
            Dispatcher.Invoke(UpdateThemeColors);
        }

        private void UpdateThemeColors()
        {
            GradientThemeStop.Color = ThemeGradientColor;
        }

        private void ColorWheelControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            double hue = GetPrivateField<double>("_currentHue");
            double saturation = GetPrivateField<double>("_currentSaturation");
            double brightness = GetPrivateField<double>("_brightness");

            SelectedColor = HsvToRgb(hue, saturation, brightness);
            ColorChanged?.Invoke(SelectedColor);
            ColorWheelControl.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    SelectedColor = ColorWheelControl.GetCurrentPreviewColor();
                    ColorChanged?.Invoke(SelectedColor);
                }
            };

            ColorWheelControl.BrightnessSlider.ValueChanged += (s, e) =>
            {
                SelectedColor = ColorWheelControl.GetCurrentPreviewColor();
                ColorChanged?.Invoke(SelectedColor);
            };

        }


        private T? GetPrivateField<T>(string fieldName)
        {
            var field = ColorWheelControl.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                return (T?)field.GetValue(ColorWheelControl);
            return default;
        }
        private Color HsvToRgb(double hue, double saturation, double value)
        {
            int hi = (int)(hue / 60) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte)value;
            byte p = (byte)(value * (1 - saturation));
            byte q = (byte)(value * (1 - f * saturation));
            byte t = (byte)(value * (1 - (1 - f) * saturation));

            return hi switch
            {
                0 => Color.FromRgb(v, t, p),
                1 => Color.FromRgb(q, v, p),
                2 => Color.FromRgb(p, v, t),
                3 => Color.FromRgb(p, q, v),
                4 => Color.FromRgb(t, p, v),
                _ => Color.FromRgb(v, p, q),
            };
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            ThemeManager.UnregisterElement(this);
            base.OnClosed(e);
        }

        private void MainBorder_MouseMove(object sender, MouseEventArgs e)
        {
            var mousePos = e.GetPosition(MainBorder);
            double centerX = MainBorder.ActualWidth / 2;
            double centerY = MainBorder.ActualHeight / 2;

            double targetAngle = Math.Atan2(mousePos.Y - centerY, mousePos.X - centerX) * (180 / Math.PI);
            double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
            if (angleDifference > 180)
                angleDifference -= 360;

            angleDifference = Math.Clamp(angleDifference, -1, 1);
            currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;

            RotaryGradient.Angle = currentGradientAngle;
        }


    }
}