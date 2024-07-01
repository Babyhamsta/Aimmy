using System.Runtime.InteropServices;

namespace Aimmy2.Class
{
    internal class ClickThroughOverlay
    {
        // Thanks to cobble (@castme) for giving me the hint :)
        // Based on: https://social.msdn.microsoft.com/Forums/en-US/a3cb7db6-5014-430f-a5c2-c9746b077d4f/click-through-windows-and-child-image-issue?forum=wpf
        // Nori

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public static void MakeClickThrough(IntPtr hwnd)
        {
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000020);
        }
    }
}