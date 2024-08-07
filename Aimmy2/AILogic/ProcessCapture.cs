using Aimmy2.AILogic.Contracts;
using Aimmy2.Extensions;
using Class;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace Aimmy2.AILogic;

public class ProcessCapture : ICapture
{
    private readonly int _processId;

    public ProcessCapture(int processId)
    {
        _processId = processId;
    }

    public Bitmap Capture(Rectangle detectionBox)
    {
        var process = Process.GetProcessById(_processId);
        var handle = process.MainWindowHandle;

        // Get window dimensions
        var windowRect = WinAPICaller.GetWindowRectangle(handle);


        // Adjust detectionBox to window dimensions
        detectionBox.X += windowRect.Left;
        detectionBox.Y += windowRect.Top;

        Bitmap bitmap = new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size, CopyPixelOperation.SourceCopy);

        return bitmap;
    }

    public Rectangle GetCaptureArea()
    {
        var process = Process.GetProcessById(_processId);
        var handle = process.MainWindowHandle;

        // Get window dimensions
        var windowRect = WinAPICaller.GetWindowRectangle(handle); 

        return new Rectangle(windowRect.Left, windowRect.Top, windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
    }

}
