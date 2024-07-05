using SharpDX.XInput;
using System.Net.Sockets;
using System.Text;

namespace Aimmy2.InputLogic;


public class GamepadSender
{
    private readonly UdpClient _udpClient;
    private readonly string _address;
    private readonly int _port;

    public GamepadSender(string address = "127.0.0.1", int port = 13000)
    {
        _udpClient = new UdpClient();
        _address = address;
        _port = port;
    }

    public void PressButton(GamepadButtonFlags button)
    {
        SendCommand(GetButtonMask(button), true);
    }

    public void ReleaseButton(GamepadButtonFlags button)
    {
        SendCommand(GetButtonMask(button), false);
    }

    public void SetTriggerValue(string trigger, float value)
    {
        int intValue = (int)(value * 255);
        if (trigger == "LT")
        {
            SendCommand(0, lt: intValue);
        }
        else if (trigger == "RT")
        {
            SendCommand(0, rt: intValue);
        }
    }

    public void SetStickValue(string stick, short x, short y)
    {
        if (stick == "LS")
        {
            SendCommand(0, lsx: x, lsy: y);
        }
        else if (stick == "RS")
        {
            SendCommand(0, rsx: x, rsy: y);
        }
    }

    private void SendCommand(int buttonMask, bool isPressed = false, int lt = 0, int rt = 0, short lsx = 0, short lsy = 0, short rsx = 0, short rsy = 0)
    {
        // Construct the command
        int buttons = isPressed ? buttonMask : 0;
        string command = $"{buttons} {lt} {rt} {lsx} {lsy} {rsx} {rsy}";

        byte[] data = Encoding.ASCII.GetBytes(command);
        _udpClient.Send(data, data.Length, _address, _port);
    }

    private int GetButtonMask(GamepadButtonFlags button)
    {
        switch (button)
        {
            case GamepadButtonFlags.A:
                return 4096; // 0x1000
            case GamepadButtonFlags.B:
                return 8192; // 0x2000
            case GamepadButtonFlags.X:
                return 16384; // 0x4000
            case GamepadButtonFlags.Y:
                return 32768; // 0x8000
            case GamepadButtonFlags.LeftShoulder:
                return 256; // 0x0100
            case GamepadButtonFlags.RightShoulder:
                return 512; // 0x0200
            case GamepadButtonFlags.LeftThumb:
                return 64; // 0x0040
            case GamepadButtonFlags.RightThumb:
                return 128; // 0x0080
            default:
                return 0;
        }
    }
}