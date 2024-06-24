namespace InputLogic
{
    internal class MouseManager
    {
        // Existing code ...

        public static bool AutoTriggerEnabled { get; set; } = false;
        public static string TargetDetectedKey { get; set; } = "LButton"; // Default to left mouse button
        public static string TargetDetectedAction { get; set; } = "SinglePress"; // Default action

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private static void PerformKeyAction(string key, string action)
        {
            byte keyCode = (byte)Enum.Parse(typeof(Keys), key);
            switch (action)
            {
                case "SinglePress":
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, 0);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                    break;
                case "DoublePress":
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, 0);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, 0);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                    break;
                case "Hold":
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, 0);
                    break;
                case "Release":
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                    break;
            }
        }

        public static void MoveCrosshair(int detectedX, int detectedY)
        {
            // Existing logic...

            if (AutoTriggerEnabled)
            {
                PerformKeyAction(TargetDetectedKey, TargetDetectedAction);
            }
        }
    }
}
