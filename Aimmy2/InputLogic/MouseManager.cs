using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.SendInputSupport;
using System.Drawing;
using System.Runtime.InteropServices;

namespace InputLogic
{
    internal class MouseManager
    {
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private static readonly PointerMotion Motion = new();
        private static string? _lastMovementPath;
        internal static void PauseMovement(long timestamp) => Motion.Pause(timestamp);
        internal static void ResetMovement() => Motion.Reset();
        public static double smoothingFactor = 0.5;
        public static bool IsEMASmoothingEnabled = false;

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static readonly Random MouseRandom = new();

        internal static (Action down, Action up) GetMouseActions()
        {
            string mouseMovementMethod = AimSettings.MouseMovementMethod;
            Action mouseDownAction;
            Action mouseUpAction;

            switch (mouseMovementMethod)
            {
                case "SendInput":
                    mouseDownAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTDOWN);
                    mouseUpAction = () => SendInputMouse.SendMouseCommand(MOUSEEVENTF_LEFTUP);
                    break;
                case "LG HUB":
                    mouseDownAction = () => LGMouse.Move(1, 0, 0, 0);
                    mouseUpAction = () => LGMouse.Move(0, 0, 0, 0);
                    break;
                case "Razer Synapse (Require Razer Peripheral)":
                    mouseDownAction = () => RZMouse.mouse_click(1);
                    mouseUpAction = () => RZMouse.mouse_click(0);
                    break;
                case "ddxoft Virtual Input Driver":
                    mouseDownAction = () => DdxoftMain.ddxoftInstance.btn!(1);
                    mouseUpAction = () => DdxoftMain.ddxoftInstance.btn(2);
                    break;
                default:
                    mouseDownAction = () => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouseUpAction = () => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }

            return (mouseDownAction, mouseUpAction);
        }

        public static void MoveCrosshair(int targetX, int targetY, int observedX, int observedY, long timestamp)
        {
            string path = AimSettings.MovementPath;
            if (_lastMovementPath != path)
            {
                Motion.Reset();
                _lastMovementPath = path;
            }

            var target = new Point(targetX, targetY);
            double smoothing = IsEMASmoothingEnabled ? smoothingFactor : 1;
            double sensitivity = AimSettings.MouseSensitivity;
            int jitter = AimSettings.MouseJitter;
            var command = MovementPaths.GetCommand(path, target, sensitivity);
            var observed = new Point(observedX, observedY);
            var newPosition = Motion.Step(command, observed, timestamp, smoothing,
                MouseRandom.Next(-jitter, jitter + 1), MouseRandom.Next(-jitter, jitter + 1));
            if (newPosition == Point.Empty) return;

            switch (AimSettings.MouseMovementMethod)
            {
                case "SendInput":
                    SendInputMouse.SendMouseCommand(MOUSEEVENTF_MOVE, newPosition.X, newPosition.Y);
                    break;

                case "LG HUB":
                    LGMouse.Move(0, newPosition.X, newPosition.Y, 0);
                    break;

                case "Razer Synapse (Require Razer Peripheral)":
                    RZMouse.mouse_move(newPosition.X, newPosition.Y, true);
                    break;

                case "ddxoft Virtual Input Driver":
                    DdxoftMain.ddxoftInstance.movR!(newPosition.X, newPosition.Y);
                    break;

                default:
                    mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);
                    break;
            }
        }
    }
}
