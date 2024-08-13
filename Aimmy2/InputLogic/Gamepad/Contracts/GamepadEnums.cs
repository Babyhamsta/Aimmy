using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using static CoreDX.vJoy.Wrapper.VJoyControllerManager;

namespace Aimmy2.InputLogic.Contracts;


public enum GamepadButton
{
    A, B, X, Y,
    LeftShoulder, RightShoulder,
    Back, Start,
    LeftThumb, RightThumb,
    Up, Down, Left, Right
}

public enum GamepadSlider
{
    LeftTrigger, RightTrigger
}

public enum GamepadAxis
{
    LeftThumbX, LeftThumbY,
    RightThumbX, RightThumbY
}

public static class GamepadEnumExtensions
{
    public static Xbox360Button ToXbox360Button(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => Xbox360Button.A,
            GamepadButton.B => Xbox360Button.B,
            GamepadButton.X => Xbox360Button.X,
            GamepadButton.Y => Xbox360Button.Y,
            GamepadButton.LeftShoulder => Xbox360Button.LeftShoulder,
            GamepadButton.RightShoulder => Xbox360Button.RightShoulder,
            GamepadButton.Back => Xbox360Button.Back,
            GamepadButton.Start => Xbox360Button.Start,
            GamepadButton.LeftThumb => Xbox360Button.LeftThumb,
            GamepadButton.RightThumb => Xbox360Button.RightThumb,
            GamepadButton.Up => Xbox360Button.Up,
            GamepadButton.Down => Xbox360Button.Down,
            GamepadButton.Left => Xbox360Button.Left,
            GamepadButton.Right => Xbox360Button.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
        };
    }

    public static Xbox360Slider ToXbox360Slider(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => Xbox360Slider.LeftTrigger,
            GamepadSlider.RightTrigger => Xbox360Slider.RightTrigger,
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null)
        };
    }

    public static Xbox360Axis ToXbox360Axis(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => Xbox360Axis.LeftThumbX,
            GamepadAxis.LeftThumbY => Xbox360Axis.LeftThumbY,
            GamepadAxis.RightThumbX => Xbox360Axis.RightThumbX,
            GamepadAxis.RightThumbY => Xbox360Axis.RightThumbY,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }

    public static GamepadButtonFlags ToGamepadButtonFlags(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => GamepadButtonFlags.A,
            GamepadButton.B => GamepadButtonFlags.B,
            GamepadButton.X => GamepadButtonFlags.X,
            GamepadButton.Y => GamepadButtonFlags.Y,
            GamepadButton.LeftShoulder => GamepadButtonFlags.LeftShoulder,
            GamepadButton.RightShoulder => GamepadButtonFlags.RightShoulder,
            GamepadButton.LeftThumb => GamepadButtonFlags.LeftThumb,
            GamepadButton.RightThumb => GamepadButtonFlags.RightThumb,
            GamepadButton.Up => GamepadButtonFlags.DPadUp,
            GamepadButton.Down => GamepadButtonFlags.DPadDown,
            GamepadButton.Left => GamepadButtonFlags.DPadLeft,
            GamepadButton.Right => GamepadButtonFlags.DPadRight,
            GamepadButton.Start => GamepadButtonFlags.Start,
            GamepadButton.Back => GamepadButtonFlags.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
    }

    public static string ToTriggerString(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => "LT",
            GamepadSlider.RightTrigger => "RT",
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null),
        };
    }

    public static string ToStickString(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => "LS",
            GamepadAxis.LeftThumbY => "LS",
            GamepadAxis.RightThumbX => "RS",
            GamepadAxis.RightThumbY => "RS",
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };
    }

    public static uint ToVJoyButton(this GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => 1,
            GamepadButton.B => 2,
            GamepadButton.X => 3,
            GamepadButton.Y => 4,
            GamepadButton.LeftShoulder => 5,
            GamepadButton.RightShoulder => 6,
            GamepadButton.Back => 7,
            GamepadButton.Start => 8,
            GamepadButton.LeftThumb => 9,
            GamepadButton.RightThumb => 10,
            GamepadButton.Up => 11,
            GamepadButton.Down => 12,
            GamepadButton.Left => 13,
            GamepadButton.Right => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
    }

    public static USAGES ToVJoyUsage(this GamepadAxis axis)
    {
        return axis switch
        {
            GamepadAxis.LeftThumbX => USAGES.X,
            GamepadAxis.LeftThumbY => USAGES.Y,
            GamepadAxis.RightThumbX => USAGES.Rx,
            GamepadAxis.RightThumbY => USAGES.Ry,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };
    }

    public static USAGES ToVJoyUsage(this GamepadSlider slider)
    {
        return slider switch
        {
            GamepadSlider.LeftTrigger => USAGES.Slider0,
            GamepadSlider.RightTrigger => USAGES.Slider1,
            // Add other mappings as needed
            _ => throw new ArgumentOutOfRangeException(nameof(slider), slider, null),
        };
    }
}