using Aimmy2.AILogic.Contracts;
using System.Drawing;

namespace Aimmy2.AILogic;

public class ScreenCapture : IScreenCapture
{
    private Bitmap? _screenCaptureBitmap;
    private Graphics? _graphics;

    public Bitmap Capture(Rectangle detectionBox)
    {
        if (_graphics == null || _screenCaptureBitmap == null || _screenCaptureBitmap.Width != detectionBox.Width || _screenCaptureBitmap.Height != detectionBox.Height)
        {
            _screenCaptureBitmap?.Dispose();
            _screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height);

            _graphics?.Dispose();
            _graphics = Graphics.FromImage(_screenCaptureBitmap);
        }

        _graphics.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size);
        return _screenCaptureBitmap;
    }
}