using System.Collections.Concurrent;
using Aimmy2.InputLogic.Contracts;
using CoreDX.vJoy.Wrapper;
using SharpDX.XInput;
using static CoreDX.vJoy.Wrapper.VJoyControllerManager;

public class GamepadSenderVJoy : IGamepadSender
{
    private Controller _physicalController;
    private IVJoyController _vJoyController;
    private readonly BlockingCollection<Action> _actions = new();
    private bool _isRunning;

    private readonly HashSet<uint> _pausedButtons = new();
    private readonly HashSet<USAGES> _pausedAxes = new();

    public GamepadSenderVJoy(uint vJoyDeviceId = 1)
    {
        var manager = VJoyControllerManager.GetManager();
        _vJoyController = manager.AcquireController(vJoyDeviceId);

        if (_vJoyController == null)
            throw new Exception($"Failed to acquire vJoy device number {vJoyDeviceId}.");
    }

    public bool CanWork => _vJoyController != null && !_vJoyController.HasRelinquished;

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
        _actions.Add(() => _pausedButtons.Add(button.ToVJoyButton()));
        return this;
    }

    public IGamepadSender PauseSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedAxes.Add(slider.ToVJoyUsage()));
        return this;
    }

    public IGamepadSender PauseSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Add(axis.ToVJoyUsage()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadButton button)
    {
        _actions.Add(() => _pausedButtons.Remove(button.ToVJoyButton()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadSlider slider)
    {
        _actions.Add(() => _pausedAxes.Remove(slider.ToVJoyUsage()));
        return this;
    }

    public IGamepadSender ResumeSync(GamepadAxis axis)
    {
        _actions.Add(() => _pausedAxes.Remove(axis.ToVJoyUsage()));
        return this;
    }

    public IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(button);

        _actions.Add(() =>
        {
            if (pressed)
                _vJoyController.PressButton(button.ToVJoyButton());
            else
                _vJoyController.ReleaseButton(button.ToVJoyButton());
        });

        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(button);

        return this;
    }

    public IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(slider);

        _actions.Add(() =>
        {
            switch (slider)
            {
                case GamepadSlider.LeftTrigger:
                    _vJoyController.SetSlider0(value * 255 / 100);
                    break;
                case GamepadSlider.RightTrigger:
                    _vJoyController.SetSlider1(value * 255 / 100);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slider), slider, null);
            }
        });

        if (gamepadSyncState == GamepadSyncState.Resume)
            ResumeSync(slider);

        return this;
    }

    public IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None)
    {
        if (gamepadSyncState == GamepadSyncState.Paused)
            PauseSync(axis);

        _actions.Add(() =>
        {
            switch (axis)
            {
                case GamepadAxis.LeftThumbX:
                    _vJoyController.SetAxisX(value);
                    break;
                case GamepadAxis.LeftThumbY:
                    _vJoyController.SetAxisY(value);
                    break;
                case GamepadAxis.RightThumbX:
                    _vJoyController.SetAxisRx(value);
                    break;
                case GamepadAxis.RightThumbY:
                    _vJoyController.SetAxisRy(value);
                    break;
            }
        });

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

            // Sync buttons
            if (!_pausedButtons.Contains(1)) _vJoyController.PressButton(1); // Button A
            if (!_pausedButtons.Contains(2)) _vJoyController.PressButton(2); // Button B
            if (!_pausedButtons.Contains(3)) _vJoyController.PressButton(3); // Button X
            if (!_pausedButtons.Contains(4)) _vJoyController.PressButton(4); // Button Y
            if (!_pausedButtons.Contains(5)) _vJoyController.PressButton(5); // Button LeftShoulder
            if (!_pausedButtons.Contains(6)) _vJoyController.PressButton(6); // Button RightShoulder
            if (!_pausedButtons.Contains(7)) _vJoyController.PressButton(7); // Button Back
            if (!_pausedButtons.Contains(8)) _vJoyController.PressButton(8); // Button Start
            if (!_pausedButtons.Contains(9)) _vJoyController.PressButton(9); // Button LeftThumb
            if (!_pausedButtons.Contains(10)) _vJoyController.PressButton(10); // Button RightThumb
            if (!_pausedButtons.Contains(11)) _vJoyController.PressButton(11); // Button DPadUp
            if (!_pausedButtons.Contains(12)) _vJoyController.PressButton(12); // Button DPadDown
            if (!_pausedButtons.Contains(13)) _vJoyController.PressButton(13); // Button DPadLeft
            if (!_pausedButtons.Contains(14)) _vJoyController.PressButton(14); // Button DPadRight

            // Sync sliders (triggers)
            if (!_pausedAxes.Contains(USAGES.Slider0)) _vJoyController.SetSlider0(state.Gamepad.LeftTrigger);
            if (!_pausedAxes.Contains(USAGES.Slider1)) _vJoyController.SetSlider1(state.Gamepad.RightTrigger);

            // Sync axes (thumbsticks)
            if (!_pausedAxes.Contains(USAGES.X)) _vJoyController.SetAxisX(state.Gamepad.LeftThumbX);
            if (!_pausedAxes.Contains(USAGES.Y)) _vJoyController.SetAxisY(state.Gamepad.LeftThumbY);
            if (!_pausedAxes.Contains(USAGES.Rx)) _vJoyController.SetAxisRx(state.Gamepad.RightThumbX);
            if (!_pausedAxes.Contains(USAGES.Ry)) _vJoyController.SetAxisRy(state.Gamepad.RightThumbY);

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        StopSync();
        _vJoyController.Dispose();
    }
}
