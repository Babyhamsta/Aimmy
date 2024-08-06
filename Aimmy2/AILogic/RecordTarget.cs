using System.Diagnostics;
using Aimmy2.Models;

namespace Aimmy2.AILogic;

public class RecordTarget
{
    public RecordTargetType TargetType { get; }
    public int? ProcessOrScreenId { get; }

    private RecordTarget(RecordTargetType targetType, int? processOrScreenId = null)
    {
        TargetType = targetType;
        ProcessOrScreenId = processOrScreenId;
    }

    public static RecordTarget MainScreen() => new(RecordTargetType.Screen);

    public static RecordTarget Screen(int index) => new(RecordTargetType.Screen, index);

    public static RecordTarget Process(int processId) => new(RecordTargetType.Process, processId);

    public static RecordTarget Process(ProcessModel process) => Process(process.Process);

    public static RecordTarget Process(Process process) => new(RecordTargetType.Process, process.Id);
}

public enum RecordTargetType
{
    Screen,
    Process
}