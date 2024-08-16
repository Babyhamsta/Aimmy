using Aimmy2.AILogic.Contracts;
using Class;
using System.Drawing;
using System.Windows.Forms;
using Visuality;

namespace Aimmy2.AILogic;

public class ScreenCapture : ICapture
{
    private Bitmap? _screenCaptureBitmap;
    private Graphics? _graphics;
    public Screen Screen { get; }

    public ScreenCapture(): this(Screen.PrimaryScreen!)
    {}

    public ScreenCapture(Screen screen)
    {
        Screen = screen;
    }

    public ScreenCapture(int screenIndex): this(Screen.AllScreens[screenIndex])
    {}

    public Bitmap Capture(Rectangle detectionBox)
    {

        if (_graphics == null || _screenCaptureBitmap == null || _screenCaptureBitmap.Width != detectionBox.Width || _screenCaptureBitmap.Height != detectionBox.Height)
        {
            _screenCaptureBitmap?.Dispose();
            _screenCaptureBitmap = new Bitmap(detectionBox.Width, detectionBox.Height);

            _graphics?.Dispose();
            _graphics = Graphics.FromImage(_screenCaptureBitmap);
        }

        _graphics.CopyFromScreen(Screen.Bounds.Left + detectionBox.Left, Screen.Bounds.Top + detectionBox.Top, 0, 0, detectionBox.Size);

        return _screenCaptureBitmap;
    }

    public Rectangle GetCaptureArea()
    {
        return Screen.Bounds;
    }
}