using SharpDX.XInput;

namespace Aimmy2.InputLogic.Contracts;

public interface IGamepadSender: IDisposable
{
    bool CanWork { get; }
    IGamepadSender SyncWith(Controller physicalController);
    IGamepadSender StopSync();
    IGamepadSender PauseSync(GamepadButton button);
    IGamepadSender PauseSync(GamepadSlider slider);
    IGamepadSender PauseSync(GamepadAxis axis);
    IGamepadSender ResumeSync(GamepadButton button);
    IGamepadSender ResumeSync(GamepadSlider slider);
    IGamepadSender ResumeSync(GamepadAxis axis);
    IGamepadSender SetButtonState(GamepadButton button, bool pressed, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
    IGamepadSender SetSliderValue(GamepadSlider slider, byte value, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
    IGamepadSender SetAxisValue(GamepadAxis axis, short value, GamepadSyncState gamepadSyncState = GamepadSyncState.None);
}
