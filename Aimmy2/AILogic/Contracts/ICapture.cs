
using System.Drawing;
using System.Windows.Forms;

namespace Aimmy2.AILogic.Contracts;

public interface ICapture
{
    Screen Screen { get; }
    Rectangle GetCaptureArea();
    Bitmap Capture(Rectangle detectionBox);
}
