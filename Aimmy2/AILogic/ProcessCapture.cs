using Aimmy2.AILogic.Contracts;
using Aimmy2.Extensions;
using Class;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.Metadata;
using System.Windows.Forms;

namespace Aimmy2.AILogic;

public class ProcessCapture : ICapture
{
    private readonly int _processId;

    public ProcessCapture(Process? process) : this(process?.Id ?? 0)
    {}

    public ProcessCapture(int processId)
    {
        if(processId == 0)
        {
            throw new ArgumentException("Process not running");
        }
        _processId = processId;
        Screen = GetProcessTargetScreen();
    }

    public Bitmap Capture(Rectangle detectionBox)
    {
        var handle = GetProcessWindowHandle();

        Screen = GetProcessTargetScreen(handle);

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
    
    private Screen GetProcessTargetScreen(IntPtr? handle = null)
    {
        handle ??= GetProcessWindowHandle();
        return Screen.FromHandle(handle.Value);
    }

    private IntPtr GetProcessWindowHandle()
    {
        var process = Process.GetProcessById(_processId);
        var handle = process.MainWindowHandle;
        return handle;
    }

    public Screen Screen { get; private set; }

    public Rectangle GetCaptureArea()
    {
        var process = Process.GetProcessById(_processId);
        var handle = process.MainWindowHandle;

        // Get window dimensions
        var windowRect = WinAPICaller.GetWindowRectangle(handle); 

        return new Rectangle(windowRect.Left, windowRect.Top, windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
    }

}
