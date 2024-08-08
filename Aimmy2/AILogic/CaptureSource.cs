using System.Diagnostics;
using System.Windows.Forms;
using Aimmy2.Models;
using Nextended.Core.Extensions;

namespace Aimmy2.AILogic;

public class CaptureSource
{
    public CaptureTargetType TargetType { get; set; }
    public int? ProcessOrScreenId { get; set; }
    public string Title { get; set; }

    public CaptureSource()
    {}

    private CaptureSource(CaptureTargetType targetType, string title, int? processOrScreenId = null)
    {
        TargetType = targetType;
        ProcessOrScreenId = processOrScreenId;
        Title = title;
    }

    public static CaptureSource MainScreen() => new(CaptureTargetType.Screen, System.Windows.Forms.Screen.PrimaryScreen.DeviceName);

    public static CaptureSource Screen(Screen screen) => new(CaptureTargetType.Screen, screen.DeviceName, System.Windows.Forms.Screen.AllScreens.IndexOf(screen));
    public static CaptureSource Screen(int index) => new(CaptureTargetType.Screen, System.Windows.Forms.Screen.AllScreens[index].DeviceName, index);

    public static CaptureSource Process(int processId) => new(CaptureTargetType.Process, System.Diagnostics.Process.GetProcessById(processId).MainWindowTitle, processId);

    public static CaptureSource Process(ProcessModel process) => Process(process.Process);

    public static CaptureSource Process(Process process) => new(CaptureTargetType.Process, process.MainWindowTitle, process.Id);
}

public enum CaptureTargetType
{
    Screen,
    Process
}