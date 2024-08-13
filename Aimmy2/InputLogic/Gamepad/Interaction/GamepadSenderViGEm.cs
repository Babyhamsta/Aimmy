using System.Collections.Concurrent;
using Aimmy2.InputLogic.Contracts;
using SharpDX.XInput;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client;



public class GamepadSenderViGEm : IGamepadSender
{
    private Controller _physicalController;
    private readonly IXbox360Controller? _virtualController;
    private bool _isRunning;
    private readonly HashSet<Xbox360Button> _pausedButtons = new();
    private readonly HashSet<Xbox360Slider> _pausedSliders = new();
    private readonly HashSet<Xbox360Axis> _pausedAxes = new();
    private readonly BlockingCollection<Action> _actions = new();
    private readonly ViGEmClient _client;

    public GamepadSenderViGEm()
    {
        _client = new ViGEmClient();
        _virtualController = _client.CreateXbox360Controller(2, ushort.MaxValue);
        _virtualController.Connect();
    }

    public bool CanWork => _virtualController != null;


    public IGamepadSender SyncWith(Controller physicalController)
    {
        _physicalController = physicalController;
      //  _physicalController.Deactivate();
        _isRunning = true;
        var thread = new Thread(SyncLoop);
        thread.Start();
        return this;
    }

    public IGamepadSender StopSync()
    {
        _isRunning = false;
      //  _physicalController.Activate();
        return this;
    }

    public IGamepadSender PauseSync(GamepadButton button)
    {
        _actions.Add(() => _pausedButtons.Add(button.ToXbox360Button()));
        return this;
    }

    public IGamepadSender PauseSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedSliders.Add(slider.ToXbox360Slider()));
        return this;
    }

    public IGamepadSender PauseSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Add(axis.ToXbox360Axis()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadButton button)
    {
        _actions.Add(() => _pausedButtons.Remove(button.ToXbox360Button()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedSliders.Remove(slider.ToXbox360Slider()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Remove(axis.ToXbox360Axis()));
        return this;
    }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(button);
        _actions.Add(() => _virtualController?.SetButtonState(button.ToXbox360Button(), pressed));
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(button);
        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(slider);
        _actions.Add(() => _virtualController?.SetSliderValue(slider.ToXbox360Slider(), value));
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(slider);
        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(axis);
        _actions.Add(() => _virtualController?.SetAxisValue(axis.ToXbox360Axis(), value));
        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(axis);
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

            if (!_pausedButtons.Contains(Xbox360Button.A)) _virtualController?.SetButtonState(Xbox360Button.A, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A));
            if (!_pausedButtons.Contains(Xbox360Button.B)) _virtualController?.SetButtonState(Xbox360Button.B, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B));
            if (!_pausedButtons.Contains(Xbox360Button.X)) _virtualController?.SetButtonState(Xbox360Button.X, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X));
            if (!_pausedButtons.Contains(Xbox360Button.Y)) _virtualController?.SetButtonState(Xbox360Button.Y, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y));
            if (!_pausedButtons.Contains(Xbox360Button.LeftShoulder)) _virtualController?.SetButtonState(Xbox360Button.LeftShoulder, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
            if (!_pausedButtons.Contains(Xbox360Button.RightShoulder)) _virtualController?.SetButtonState(Xbox360Button.RightShoulder, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder));
            if (!_pausedButtons.Contains(Xbox360Button.Back)) _virtualController?.SetButtonState(Xbox360Button.Back, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back));
            if (!_pausedButtons.Contains(Xbox360Button.Start)) _virtualController?.SetButtonState(Xbox360Button.Start, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start));
            if (!_pausedButtons.Contains(Xbox360Button.LeftThumb)) _virtualController?.SetButtonState(Xbox360Button.LeftThumb, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb));
            if (!_pausedButtons.Contains(Xbox360Button.RightThumb)) _virtualController?.SetButtonState(Xbox360Button.RightThumb, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb));
            if (!_pausedButtons.Contains(Xbox360Button.Up)) _virtualController?.SetButtonState(Xbox360Button.Up, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp));
            if (!_pausedButtons.Contains(Xbox360Button.Down)) _virtualController?.SetButtonState(Xbox360Button.Down, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown));
            if (!_pausedButtons.Contains(Xbox360Button.Left)) _virtualController?.SetButtonState(Xbox360Button.Left, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft));
            if (!_pausedButtons.Contains(Xbox360Button.Right)) _virtualController?.SetButtonState(Xbox360Button.Right, state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight));

            if (!_pausedSliders.Contains(Xbox360Slider.LeftTrigger)) _virtualController?.SetSliderValue(Xbox360Slider.LeftTrigger, state.Gamepad.LeftTrigger);
            if (!_pausedSliders.Contains(Xbox360Slider.RightTrigger)) _virtualController?.SetSliderValue(Xbox360Slider.RightTrigger, state.Gamepad.RightTrigger);

            if (!_pausedAxes.Contains(Xbox360Axis.LeftThumbX)) _virtualController?.SetAxisValue(Xbox360Axis.LeftThumbX, state.Gamepad.LeftThumbX);
            if (!_pausedAxes.Contains(Xbox360Axis.LeftThumbY)) _virtualController?.SetAxisValue(Xbox360Axis.LeftThumbY, state.Gamepad.LeftThumbY);
            if (!_pausedAxes.Contains(Xbox360Axis.RightThumbX)) _virtualController?.SetAxisValue(Xbox360Axis.RightThumbX, state.Gamepad.RightThumbX);
            if (!_pausedAxes.Contains(Xbox360Axis.RightThumbY)) _virtualController?.SetAxisValue(Xbox360Axis.RightThumbY, state.Gamepad.RightThumbY);

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        StopSync();
        _virtualController?.Disconnect();
        _client.Dispose();
    }
}
