using System.Drawing;

namespace Aimmy2.AILogic
{
    internal static class CaptureTargetSelector
    {
        internal static (Rectangle Source, Point Destination) VisibleRegion(Rectangle crop, Rectangle display)
        {
            Rectangle visible = Rectangle.Intersect(display, crop);
            return (visible, new Point(visible.Left - crop.Left, visible.Top - crop.Top));
        }

        internal static Rectangle SelectDetectionBox(
            string detectionAreaType,
            int imageSize,
            Rectangle displayBounds,
            Point mousePosition,
            bool mouseOnCurrentDisplay)
        {
            int centerX;
            int centerY;

            if (detectionAreaType == "Closest to Mouse" && mouseOnCurrentDisplay)
            {
                centerX = mousePosition.X;
                centerY = mousePosition.Y;
            }
            else
            {
                centerX = displayBounds.Left + (displayBounds.Width / 2);
                centerY = displayBounds.Top + (displayBounds.Height / 2);
            }

            return new Rectangle(
                centerX - imageSize / 2,
                centerY - imageSize / 2,
                imageSize,
                imageSize);
        }
    }
}