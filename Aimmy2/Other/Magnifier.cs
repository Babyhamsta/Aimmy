using System.Drawing;
using System.Drawing.Drawing2D;

namespace Aimmy2.Other;


public class Magnifier
{
    public static Bitmap ZoomArea(Rectangle captureArea, double zoomFactor)
    {
        // Calculate the center of the capture area
        int centerX = captureArea.Left + captureArea.Width / 2;
        int centerY = captureArea.Top + captureArea.Height / 2;

        // Calculate the capture rectangle based on the zoom factor
        int captureWidth = (int)(captureArea.Width / zoomFactor);
        int captureHeight = (int)(captureArea.Height / zoomFactor);
        int captureX = centerX - captureWidth / 2;
        int captureY = centerY - captureHeight / 2;

        // Capture the screen area
        Bitmap screenCapture = new Bitmap(captureWidth, captureHeight);
        using (Graphics g = Graphics.FromImage(screenCapture))
        {
            g.CopyFromScreen(captureX, captureY, 0, 0, new Size(captureWidth, captureHeight));
        }

        // Create a bitmap scaled by the zoom factor
        Bitmap zoomedBitmap = new Bitmap((int)(captureArea.Width), (int)(captureArea.Height));
        zoomedBitmap.Save("D:\\test.png");
        using (Graphics g = Graphics.FromImage(zoomedBitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(screenCapture, new Rectangle(0, 0, zoomedBitmap.Width, zoomedBitmap.Height));
        }

        return zoomedBitmap;
    }
}