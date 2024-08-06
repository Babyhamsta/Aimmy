
using System.Drawing;

namespace Aimmy2.AILogic.Contracts;

public interface ICapture
{
    Rectangle GetCaptureArea();
    Bitmap Capture(Rectangle detectionBox);
}
