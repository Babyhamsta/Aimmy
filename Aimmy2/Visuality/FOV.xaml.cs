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

        private Rect _windowBounds;
        private bool _followingCursor;

        public FOV()
        {
            InitializeComponent();
            IsVisibleChanged += (_, _) => UpdateCursorTracking();

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

            ForceReposition();
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
            if (DisplayManager.CurrentDisplay is not { } display) return;
            var bounds = display.Bounds;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && _windowBounds != bounds &&
                SetWindowPos(hwnd, IntPtr.Zero, (int)bounds.X, (int)bounds.Y,
                    (int)bounds.Width, (int)bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE))
                _windowBounds = bounds;

            SetCenter(new System.Drawing.Point((int)(bounds.X + bounds.Width / 2),
                (int)(bounds.Y + bounds.Height / 2)));
            UpdateFOVSize(AimSettings.FovSize);
            UpdateCursorTracking();
        }

        internal void SetCenter(System.Drawing.Point center)
        {
            if (_windowBounds.IsEmpty || _windowBounds.Width == 0) return;
            var dpi = VisualTreeHelper.GetDpi(this);
            double x = (center.X - _windowBounds.X - _windowBounds.Width / 2) / dpi.DpiScaleX;
            double y = (center.Y - _windowBounds.Y - _windowBounds.Height / 2) / dpi.DpiScaleY;
            if (RingOffset.X != x) RingOffset.X = x;
            if (RingOffset.Y != y) RingOffset.Y = y;
        }

        private void UpdateCursorTracking()
        {
            bool follow = IsVisible && AimSettings.DetectionAreaType == "Closest to Mouse";
            if (_followingCursor == follow) return;
            _followingCursor = follow;
            if (follow)
                CompositionTarget.Rendering += FollowCursor;
            else
                CompositionTarget.Rendering -= FollowCursor;
        }

        private void FollowCursor(object? sender, EventArgs e)
        {
            var cursor = WinAPICaller.GetCursorPosition();
            if (_windowBounds.Contains(new Point(cursor.X, cursor.Y)))
                SetCenter(cursor);
        }

        private void UpdateFOVColor(Color newColor)
        {
            var brush = new SolidColorBrush(newColor);
            Circle.Stroke = brush;
            RectangleShape.Stroke = brush;
        }


        public void UpdateFOVSize(double newdouble)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            Circle.Width = RectangleShape.Width = newdouble / dpi.DpiScaleX;
            Circle.Height = RectangleShape.Height = newdouble / dpi.DpiScaleY;
        }


        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= FollowCursor;
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}