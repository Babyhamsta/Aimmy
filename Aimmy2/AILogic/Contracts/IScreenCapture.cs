
using System.Drawing;

namespace Aimmy2.AILogic.Contracts;

public interface IScreenCapture
{
    Bitmap Capture(Rectangle detectionBox);
}
