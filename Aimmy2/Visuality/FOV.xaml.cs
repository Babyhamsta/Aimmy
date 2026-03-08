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
    public partial class FOV : Window
    {
        // Windows API for forcing window position
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;

        public FOV()
        {
            InitializeComponent();

            // Subscribe to display changes early
            DisplayManager.DisplayChanged += OnDisplayChanged;

            //Subscribe to Exclusion (I love thick latinas btw)
            ThemeManager.ExcludeWindowFromBackground(this);

            // Subscribe to property changes
            PropertyChanger.ReceiveColor = UpdateFOVColor;
            PropertyChanger.ReceiveFOVSize = UpdateFOVSize;
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

                // Center the FOV circle on the current display
                var centerX = (DisplayManager.ScreenWidth / 2.0) / WinAPICaller.scalingFactorX;
                var centerY = (DisplayManager.ScreenHeight / 2.0) / WinAPICaller.scalingFactorY;

                FOVStrictEnclosure.Margin = new Thickness(
                    centerX - 320,  // 320 = half of 640 (FOV size)
                    centerY - 320,
                    0, 0);

                // Force layout update
                this.UpdateLayout();

            }
            catch
            {
            }
        }

        private void UpdateFOVColor(Color newColor)
        {
            var brush = new SolidColorBrush(newColor);
            Circle.Stroke = brush;
            RectangleShape.Stroke = brush;
        }


        public void UpdateFOVSize(double newdouble)
        {
            Circle.Width = Circle.Height = newdouble;
            RectangleShape.Width = RectangleShape.Height = newdouble;
        }


        protected override void OnClosed(EventArgs e)
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}