using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Aimmy2.InputLogic.Contracts;
using SharpDX.XInput;

public class GamepadSenderXInputEmu : IGamepadSender
{
    private UdpClient? _udpClient;
    private readonly string _address;
    private readonly int _port;
    private Controller _physicalController;
    private bool _isRunning;
    private readonly BlockingCollection<Action> _actions = new();

    public GamepadSenderXInputEmu(string address = "127.0.0.1", int port = 13000)
    {
        _udpClient = new UdpClient();
        _address = address;
        _port = port;
    }

    public bool CanWork => true; // Assuming the UDP connection always works for simplicity

    public IGamepadSender SyncWith(Controller physicalController)
    {
        _physicalController = physicalController;
        _isRunning = true;
        var thread = new Thread(SyncLoop);
        thread.Start();
        return this;
    }

    public IGamepadSender StopSync()
    {
        _isRunning = false;
        return this;
    }

    public IGamepadSender PauseSync(GamepadButton button)
    {
        _actions.Add(() => ReleaseButton(button.ToGamepadButtonFlags()));
        return this;
    }

    public IGamepadSender PauseSync(GamepadSlider slider)
    {
        _actions.Add(() => SetTriggerValue(slider.ToTriggerString(), 0));
        return this;
    }

    public IGamepadSender PauseSync(GamepadAxis axis)
    {
        _actions.Add(() => SetStickValue(axis.ToStickString(), 0, 0));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadButton button)
    {
        // No-op for XInputEmu as it directly interacts with the buttons and axes
        return this;
    }

    public IGamepadSender ResumeSync(GamepadSlider slider)
    {
        // No-op for XInputEmu as it directly interacts with the buttons and axes
        return this;
    }

    public IGamepadSender ResumeSync(GamepadAxis axis)
    {
        // No-op for XInputEmu as it directly interacts with the buttons and axes
        return this;
    }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (pressed)
            PressButton(button.ToGamepadButtonFlags());
        else
            ReleaseButton(button.ToGamepadButtonFlags());
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        SetTriggerValue(slider.ToTriggerString(), value / 255.0f);
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        string stick = axis.ToStickString();
        short x = axis == GamepadAxis.LeftThumbX || axis == GamepadAxis.RightThumbX ? value : (short)0;
        short y = axis == GamepadAxis.LeftThumbY || axis == GamepadAxis.RightThumbY ? value : (short)0;

        SetStickValue(stick, x, y);
        return this;
    }

    private void SyncLoop()
    {
        while (_isRunning)
        {
            var state = _physicalController.GetState();

            while (_actions.TryTake(out var action, 0))
            {
                action();
            }

            // Map and sync buttons
            MapButtonState(state, GamepadButtonFlags.A, GamepadButton.A);
            MapButtonState(state, GamepadButtonFlags.B, GamepadButton.B);
            MapButtonState(state, GamepadButtonFlags.X, GamepadButton.X);
            MapButtonState(state, GamepadButtonFlags.Y, GamepadButton.Y);

            MapButtonState(state, GamepadButtonFlags.LeftShoulder, GamepadButton.LeftShoulder);
            MapButtonState(state, GamepadButtonFlags.RightShoulder, GamepadButton.RightShoulder);

            MapButtonState(state, GamepadButtonFlags.DPadUp, GamepadButton.Up);
            MapButtonState(state, GamepadButtonFlags.DPadDown, GamepadButton.Down);
            MapButtonState(state, GamepadButtonFlags.DPadLeft, GamepadButton.Left);
            MapButtonState(state, GamepadButtonFlags.DPadRight, GamepadButton.Right);

            MapButtonState(state, GamepadButtonFlags.Start, GamepadButton.Start);
            MapButtonState(state, GamepadButtonFlags.Back, GamepadButton.Back);

            MapTriggerValue(state.Gamepad.LeftTrigger, GamepadSlider.LeftTrigger);
            MapTriggerValue(state.Gamepad.RightTrigger, GamepadSlider.RightTrigger);

            MapStickValue(state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY, "LS");
            MapStickValue(state.Gamepad.RightThumbX, state.Gamepad.RightThumbY, "RS");

            Thread.Sleep(1);
        }
    }

    private void MapButtonState(State state, GamepadButtonFlags flag, GamepadButton button)
    {
        if (state.Gamepad.Buttons.HasFlag(flag))
        {
            PressButton(flag);
        }
        else
        {
            ReleaseButton(flag);
        }
    }

    private void MapTriggerValue(byte triggerValue, GamepadSlider slider)
    {
        SetTriggerValue(slider.ToTriggerString(), triggerValue / 255.0f);
    }

    private void MapStickValue(short x, short y, string stick)
    {
        SetStickValue(stick, x, y);
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

    private void PressButton(GamepadButtonFlags button)
    {
        SendCommand(GetButtonMask(button), true);
    }

    private void ReleaseButton(GamepadButtonFlags button)
    {
        SendCommand(GetButtonMask(button), false);
    }

    private void SendCommand(int buttonMask, bool isPressed = false, int lt = 0, int rt = 0, short lsx = 0, short lsy = 0, short rsx = 0, short rsy = 0)
    {
        if (_udpClient != null)
        {
            try
            {
                int buttons = isPressed ? buttonMask : 0;
                string command = $"{buttons} {lt} {rt} {lsx} {lsy} {rsx} {rsy}";

                byte[] data = Encoding.ASCII.GetBytes(command);
                _udpClient.Send(data, data.Length, _address, _port);
            }
            catch (Exception e)
            { }
        }
    }

    private int GetButtonMask(GamepadButtonFlags button)
    {
        return button switch
        {
            GamepadButtonFlags.A => 4096, // 0x1000
            GamepadButtonFlags.B => 8192, // 0x2000
            GamepadButtonFlags.X => 16384, // 0x4000
            GamepadButtonFlags.Y => 32768, // 0x8000
            GamepadButtonFlags.LeftShoulder => 256, // 0x0100
            GamepadButtonFlags.RightShoulder => 512, // 0x0200
            GamepadButtonFlags.LeftThumb => 64, // 0x0040
            GamepadButtonFlags.RightThumb => 128, // 0x0080
            GamepadButtonFlags.DPadUp => 1, // 0x0001
            GamepadButtonFlags.DPadDown => 2, // 0x0002
            GamepadButtonFlags.DPadLeft => 4, // 0x0004
            GamepadButtonFlags.DPadRight => 8, // 0x0008
            GamepadButtonFlags.Start => 16, // 0x0010
            GamepadButtonFlags.Back => 32, // 0x0020
            _ => 0,
        };
    }

    public void Dispose()
    {
        StopSync();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        _actions.Dispose();
    }
}
