using System.Runtime.InteropServices;

namespace WinformsReplacement
{
    /// <summary>
    /// From: https://stackoverflow.com/questions/254197/how-can-i-get-the-active-screen-dimensions
    /// Nori
    /// </summary>
    public static class NativeMethods
    {
        public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, NativeMonitorInfo lpmi);

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct NativeRectangle(int left, int top, int right, int bottom)
        {
            public int Left = left;
            public int Top = top;
            public int Right = right;
            public int Bottom = bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NativeMonitorInfo
        {
            public int Size;
            public NativeRectangle Monitor;
            public NativeRectangle Work;
            public int Flags;
        }
    }
}