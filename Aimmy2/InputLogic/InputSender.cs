using System.Runtime.InteropServices;

namespace Aimmy2.InputLogic;


public class InputSender
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    public static void SendKey(dynamic key)
    {
        if (key == "Middle" || key == "Left" || key == "Right")
        {
            SendMouseClick(key);
        }
        else
        {
            SendKeyboardKey(key);
        }
    }

    private static void SendMouseClick(string button)
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[1].type = INPUT_MOUSE;

        switch (button)
        {
            case "Left":
                inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                inputs[1].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
                break;
            case "Right":
                inputs[0].u.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
                inputs[1].u.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
                break;
            case "Middle":
                inputs[0].u.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN;
                inputs[1].u.mi.dwFlags = MOUSEEVENTF_MIDDLEUP;
                break;
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SendKeyboardKey(string key)
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)key[0]; // Assuming the key is a single character string
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = (ushort)key[0];
        inputs[1].u.ki.dwFlags = 0x0002; // KEYEVENTF_KEYUP

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}