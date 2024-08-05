using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic.CompilerServices;
using Point = System.Drawing.Point;

namespace InputLogic
{
    internal class MouseManager
    {

        private static readonly double ScreenWidth = WinAPICaller.ScreenWidth;
        private static readonly double ScreenHeight = WinAPICaller.ScreenHeight;

        private static DateTime LastClickTime = DateTime.MinValue;
        private static int LastAntiRecoilClickTime = 0;

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const int WHEEL_DELTA = 120; 

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private static double previousX = 0;
        private static double previousY = 0;
        public static double smoothingFactor => AppConfig.Current.SliderSettings.EMASmoothening;
        public static bool IsEMASmoothingEnabled => AppConfig.Current.ToggleState.EMASmoothening;

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static Random MouseRandom = new();

        private static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            double x = uu * u * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + tt * t * end.X;
            double y = uu * u * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + tt * t * end.Y;

            if (IsEMASmoothingEnabled)
            {
                x = EmaSmoothing(previousX, x, smoothingFactor);
                y = EmaSmoothing(previousY, y, smoothingFactor);
            }

            return new Point((int)x, (int)y);
        }

        private static double EmaSmoothing(double previousValue, double currentValue, double smoothingFactor) => (currentValue * smoothingFactor) + (previousValue * (1 - smoothingFactor));
        public static bool IsLeftDown
        {
            get
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    return _leftDown || Mouse.LeftButton == MouseButtonState.Pressed;
                }

                return _leftDown || Application.Current.Dispatcher.Invoke(() => Mouse.LeftButton == MouseButtonState.Pressed);
            }
        }
        private static bool _leftDown;
        public static void ScrollMouseWheel(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, 0);
        }

        public static async Task ScrollMouseWheelUpAndDown()
        {
            ScrollMouseWheel(WHEEL_DELTA); // Scroll up
            await Task.Delay(20); // Optional: Ein kleiner Delay zwischen den Scrolls
            ScrollMouseWheel(-WHEEL_DELTA); // Scroll down
        }

        public static void LeftDown()
        {
            if (IsLeftDown)
                return;
            
            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTDOWN);
                    return;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(1, 0, 0, 0);
                    return;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_click(1);
                    return;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.btn!(1);
                    return;

                default:
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    break;
            }

        }

        public static async Task LeftDownUntil(Func<Task<bool>> condition, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        {
            LeftDown();

            try
            {
                while (!await condition() && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5, cancellationToken);
                }

                if(delay.HasValue)
                    await Task.Delay(delay.Value, cancellationToken);
            }
            catch 
            {}

            LeftUp();
            _leftDown = true;
            Task.Delay(1000).ContinueWith(_ =>
            {
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                _leftDown = false;
            });
            LastClickTime = DateTime.UtcNow;
        }

        public static void LeftUp()
        {
            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTUP);
                    return;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, 0, 0, 0);
                    return;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_click(0);
                    return;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.btn(2);
                    return;

                default:
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }

            _leftDown = false;
        }

        public static async Task DoTriggerClick()
        {
            const int clickDelayMilliseconds = 20;

            LeftDown();
            await Task.Delay(clickDelayMilliseconds);
            LeftUp();

            LastClickTime = DateTime.UtcNow;
        }

        public static void DoAntiRecoil()
        {
            int timeSinceLastClick = Math.Abs(DateTime.UtcNow.Millisecond - LastAntiRecoilClickTime);

            if (timeSinceLastClick < AppConfig.Current.AntiRecoilSettings.FireRate)
            {
                return;
            }

            int xRecoil = (int)AppConfig.Current.AntiRecoilSettings.XRecoil;
            int yRecoil = (int)AppConfig.Current.AntiRecoilSettings.YRecoil;

            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, xRecoil, yRecoil);
                    break;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, xRecoil, yRecoil, 0);
                    break;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_move(xRecoil, yRecoil, true);
                    break;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.movR!(xRecoil, yRecoil);
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)xRecoil, (uint)yRecoil, 0, 0);
                    break;
            }

            LastAntiRecoilClickTime = DateTime.UtcNow.Millisecond;
        }

        public static void MoveCrosshair(int detectedX, int detectedY)
        {
            int halfScreenWidth = (int)ScreenWidth / 2;
            int halfScreenHeight = (int)ScreenHeight / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            double aspectRatioCorrection = ScreenWidth / ScreenHeight;

            int MouseJitter = (int)AppConfig.Current.SliderSettings.MouseJitter;
            int jitterX = MouseRandom.Next(-MouseJitter, MouseJitter);
            int jitterY = MouseRandom.Next(-MouseJitter, MouseJitter);

            Point start = new(0, 0);
            Point end = new(targetX, targetY);
            Point control1 = new(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
            Point control2 = new(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);
            Point newPosition = CubicBezier(start, end, control1, control2, 1 - AppConfig.Current.SliderSettings.MouseSensitivity);

            targetX = Math.Clamp(targetX, -150, 150);
            targetY = Math.Clamp(targetY, -150, 150);

            targetY = (int)(targetY * aspectRatioCorrection);

            targetX += jitterX;
            targetY += jitterY;

            switch (AppConfig.Current.DropdownState.MouseMovementMethod)
            {
                case MouseMovementMethod.SendInput:
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, newPosition.X, newPosition.Y);
                    break;

                case MouseMovementMethod.LGHUB:
                    LGMouse.Move(0, newPosition.X, newPosition.Y, 0);
                    break;

                case MouseMovementMethod.RazerSynapse:
                    RZMouse.mouse_move(newPosition.X, newPosition.Y, true);
                    break;

                case MouseMovementMethod.ddxoft:
                    DdxoftMain.ddxoftInstance.movR!(newPosition.X, newPosition.Y);
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;
            }

        }
    }
}
