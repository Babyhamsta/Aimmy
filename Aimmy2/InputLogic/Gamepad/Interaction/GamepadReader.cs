using SharpDX.XInput;
using System.Threading;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.InputLogic.Contracts;
using Newtonsoft.Json;

namespace Aimmy2.InputLogic.Gamepad.Interaction;



public class GamepadReader : IGamepadReader
{
    private readonly UserIndex _userIndex;
    public Controller Controller { get; private set; }
    public State State { get; private set; }

    private CancellationTokenSource _cancellationTokenSource;
    private Task _pollingTask;
    private readonly TaskScheduler _scheduler;

    public event EventHandler<GamepadEventArgs> ButtonEvent;

    public GamepadReader(UserIndex userIndex = UserIndex.One)
    {
        _userIndex = userIndex;
        _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        Controller = new Controller(_userIndex);

        StartPolling();
    }

    public bool IsConnected => Controller.IsConnected;
    public bool IsAPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
    public bool IsBPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
    public bool IsXPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
    public bool IsYPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
    public bool IsLBPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
    public bool IsRBPressed => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
    public bool IsRS => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
    public bool IsLS => IsConnected && State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
    public bool IsLTPressed => LTValue > 0;
    public bool IsRTPressed => RTValue > 0;
    public float RTValue => IsConnected ? State.Gamepad.RightTrigger / 255.0f : 0;
    public float LTValue => IsConnected ? State.Gamepad.LeftTrigger / 255.0f : 0;
    public short RSX => State.Gamepad.RightThumbX;
    public short RSY => State.Gamepad.RightThumbY;
    public short LSX => State.Gamepad.LeftThumbX;
    public short LSY => State.Gamepad.LeftThumbY;

    public bool IsPressed(string button)
    {
        return button switch
        {
            "A" => IsAPressed,
            "B" => IsBPressed,
            "X" => IsXPressed,
            "Y" => IsYPressed,
            "LB" => IsLBPressed,
            "RB" => IsRBPressed,
            "RT" => IsRTPressed,
            "LT" => IsLTPressed,
            "RS" => IsRS,
            "LS" => IsLS,
            _ => false
        };
    }

    private void StartPolling()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        _pollingTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (IsConnected)
                {
                    Poll();
                }
                else
                {
                    await Task.Delay(1000, token);
                    Controller = new Controller(_userIndex); // Retry connecting
                }
                await Task.Delay(10, token); // Reduce CPU usage
            }
        }, token);
    }

    public void StopPolling()
    {
        _cancellationTokenSource.Cancel();
        if (_pollingTask is { IsCompleted: false, IsCanceled: false })
        {
            try
            {
                _pollingTask.Wait();
            }
            catch (Exception e)
            {}
        }
    }

    private void Poll()
    {
        var newState = Controller.GetState();

        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A), "A");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B), "B");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X), "X");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y), "Y");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder), "LB");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder), "RB");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb), "RS");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb), State.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb), "LS");

        CheckTriggerState(newState.Gamepad.RightTrigger, State.Gamepad.RightTrigger, "RT");
        CheckTriggerState(newState.Gamepad.LeftTrigger, State.Gamepad.LeftTrigger, "LT");

        CheckStickState(newState.Gamepad.RightThumbX, State.Gamepad.RightThumbX, "RSX");
        CheckStickState(newState.Gamepad.RightThumbY, State.Gamepad.RightThumbY, "RSY");
        CheckStickState(newState.Gamepad.LeftThumbX, State.Gamepad.LeftThumbX, "LSX");
        CheckStickState(newState.Gamepad.LeftThumbY, State.Gamepad.LeftThumbY, "LSY");

        State = newState;
    }

    private void CheckButtonState(bool newState, bool oldState, string buttonName)
    {
        if (newState && !oldState)
        {
            InvokeEvent(new GamepadEventArgs { Button = buttonName, IsPressed = true });
        }
        else if (!newState && oldState)
        {
            InvokeEvent(new GamepadEventArgs { Button = buttonName, IsPressed = false });
        }
    }

    private void CheckTriggerState(byte newState, byte oldState, string triggerName)
    {
        if (newState != oldState)
        {
            var value = newState / 255.0f;
            var minValue = triggerName == "LT" ? AppConfig.Current.SliderSettings.GamepadMinimumLT : AppConfig.Current.SliderSettings.GamepadMinimumRT;
            var isPressed = value >= minValue;
            InvokeEvent(new GamepadEventArgs { Button = triggerName, IsPressed = isPressed, Value = value });
        }
    }

    private void CheckStickState(short newState, short oldState, string stickName)
    {
        if (newState != oldState)
        {
            InvokeEvent(new GamepadEventArgs { Button = stickName, Value = newState, IsStickEvent = true });
        }
    }

    private void InvokeEvent(GamepadEventArgs args)
    {
        Task.Factory.StartNew(() => ButtonEvent?.Invoke(this, args), CancellationToken.None, TaskCreationOptions.None, _scheduler);
    }

    
    public void Dispose()
    {
        StopPolling();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _pollingTask?.Dispose();
    }
}