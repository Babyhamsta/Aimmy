using Class;
using System.Drawing;

namespace WinformsReplacement
{
    public class GetScalingFactor
    {
        private static Graphics graphics = Graphics.FromHwnd(IntPtr.Zero);

        // Get the scaling factor
        public static float scalingFactorX = graphics.DpiX / 96f;

        public static float scalingFactorY = graphics.DpiY / 96f;

        // Calculate the scaled width and height
        public static int scaledWidth = (int)(WinAPICaller.ScreenWidth * scalingFactorX);

        public static int scaledHeight = (int)(WinAPICaller.ScreenWidth * scalingFactorY);
    }
}