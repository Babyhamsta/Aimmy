using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using static WinformsReplacement.NativeMethods;

namespace Class
{
    public partial class WinAPICaller
    {
        #region Variables

        // Below is written by ChatGPT 3.5
        // Nori

        private static Graphics GraphicsThing = Graphics.FromHwnd(IntPtr.Zero);

        public static float scalingFactorX = GraphicsThing.DpiX / (float)96;
        public static float scalingFactorY = GraphicsThing.DpiY / (float)96;


        #endregion Variables

        #region P/Invoke signatures

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        #endregion P/Invoke signatures

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion Structures

        #region Functions


        public static System.Drawing.Point GetCursorPosition()
        {
            if (GetCursorPos(out POINT lpPoint))
            {
                return new System.Drawing.Point(lpPoint.X, lpPoint.Y);
            }
            else
            {
                // Handle the case when GetCursorPos fails
                throw new Exception("Failed to get cursor position.");
            }
        }

        public static RECT GetWindowRectangle(IntPtr hWnd)
        {
            RECT rect = new RECT();
            if (!GetWindowRect(hWnd, ref rect))
            {
                throw new Exception("Failed to get window rectangle.");
            }
            return rect;
        }

        #endregion Functions
    }
}
