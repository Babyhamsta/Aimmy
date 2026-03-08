using Aimmy2.Class;
using Aimmy2.Theme;
using Class;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Visuality
{
    public partial class DetectedPlayerWindow : Window
    {
        // Windows API for forcing window position
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;

        public DetectedPlayerWindow()
        {
            InitializeComponent();

            //Subscribe to my Onlyfans to exclude bad Behavior!
            ThemeManager.ExcludeWindowFromBackground(this);

            Title = "";

            // Subscribe to display changes early
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Subscribe to property changes
            PropertyChanger.ReceiveDPColor = UpdateDPColor;
            PropertyChanger.ReceiveDPFontSize = UpdateDPFontSize;
            PropertyChanger.ReceiveDPWCornerRadius = ChangeCornerRadius;
            PropertyChanger.ReceiveDPWBorderThickness = ChangeBorderThickness;
            PropertyChanger.ReceiveDPWOpacity = ChangeOpacity;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);

            // Now that we have a window handle, position the window
            if (!_isInitialized)
            {
                _isInitialized = true;
                ForceReposition();
            }
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {

            // Update position when display changes
            Application.Current.Dispatcher.Invoke(() =>
            {
                ForceReposition();
            });
        }

        public void ForceReposition()
        {
            try
            {

                // Get window handle
                var hwnd = _isInitialized ? new WindowInteropHelper(this).Handle : IntPtr.Zero;

                // Set window state to normal first
                this.WindowState = WindowState.Normal;

                // Position window to cover the current display (accounting for DPI scaling)
                this.Left = DisplayManager.ScreenLeft / WinAPICaller.scalingFactorX;
                this.Top = DisplayManager.ScreenTop / WinAPICaller.scalingFactorY;
                this.Width = DisplayManager.ScreenWidth / WinAPICaller.scalingFactorX;
                this.Height = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;

                // Force position with Windows API if we have a handle
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero,
                        DisplayManager.ScreenLeft,
                        DisplayManager.ScreenTop,
                        DisplayManager.ScreenWidth,
                        DisplayManager.ScreenHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                // Maximize to cover entire display
                this.WindowState = WindowState.Maximized;

                // Update tracer start position (changed to be dynamic)
                DetectedTracers.X1 = (DisplayManager.ScreenWidth / 2.0) / WinAPICaller.scalingFactorX;

                string tracerPosition = "Bottom"; // default value
                if (Dictionary.dropdownState.TryGetValue("Tracer Position", out var position))
                {
                    tracerPosition = position.ToString();
                }

                switch (tracerPosition)
                {
                    case "Bottom":
                        DetectedTracers.Y1 = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;
                        break;
                    case "Middle":
                        DetectedTracers.Y1 = (DisplayManager.ScreenHeight / 2.0) / WinAPICaller.scalingFactorY;
                        break;
                    case "Top":
                        DetectedTracers.Y1 = 0;
                        break;
                }

                // Force layout update
                this.UpdateLayout();

            }
            catch
            {
            }
        }

        private void UpdateDPColor(Color NewColor)
        {
            DetectedPlayerFocus.BorderBrush = new SolidColorBrush(NewColor);
            DetectedPlayerConfidence.Foreground = new SolidColorBrush(NewColor);
            DetectedTracers.Stroke = new SolidColorBrush(NewColor);
        }

        private void UpdateDPFontSize(int newint) => DetectedPlayerConfidence.FontSize = newint;

        private void ChangeCornerRadius(int newint) => DetectedPlayerFocus.CornerRadius = new CornerRadius(newint);

        private void ChangeBorderThickness(double newdouble)
        {
            DetectedPlayerFocus.BorderThickness = new Thickness(newdouble);
            DetectedTracers.StrokeThickness = newdouble;
        }

        private void ChangeOpacity(double newdouble) => DetectedPlayerFocus.Opacity = newdouble;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // Clean up event subscription
        protected override void OnClosed(EventArgs e)
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}