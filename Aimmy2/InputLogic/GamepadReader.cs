using SharpDX.XInput;
using System.Threading;
using Aimmy2.Class;
using Newtonsoft.Json;

namespace Aimmy2.InputLogic;

public class GamepadReader: IDisposable
{
    private Controller _controller;
    private State _state;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _pollingTask;
    private readonly TaskScheduler _scheduler;

    public event EventHandler<GamepadEventArgs> ButtonEvent;

    public GamepadReader()
    {
        _scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        _controller = new Controller(UserIndex.One);

        StartPolling();
    }

    public bool IsConnected => _controller.IsConnected;
    public bool IsAPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
    public bool IsBPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
    public bool IsXPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
    public bool IsYPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
    public bool IsLBPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
    public bool IsRBPressed => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
    public bool IsRS => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
    public bool IsLS => IsConnected && _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
    public bool IsLTPressed => LTValue > 0;
    public bool IsRTPressed => RTValue > 0;
    public float RTValue => IsConnected ? _state.Gamepad.RightTrigger / 255.0f : 0;
    public float LTValue => IsConnected ? _state.Gamepad.LeftTrigger / 255.0f : 0;
    public short RSX => _state.Gamepad.RightThumbX;
    public short RSY => _state.Gamepad.RightThumbY;
    public short LSX => _state.Gamepad.LeftThumbX;
    public short LSY => _state.Gamepad.LeftThumbY;

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
                    _controller = new Controller(UserIndex.One); // Retry connecting
                }
                await Task.Delay(100, token); // Reduce CPU usage
            }
        }, token);
    }

    public void StopPolling()
    {
        _cancellationTokenSource.Cancel();
        _pollingTask.Wait();
    }

    private void Poll()
    {
        var newState = _controller.GetState();

        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A), "A");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B), "B");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X), "X");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y), "Y");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder), "LB");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder), "RB");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb), "RS");
        CheckButtonState(newState.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb), _state.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb), "LS");

        CheckTriggerState(newState.Gamepad.RightTrigger, _state.Gamepad.RightTrigger, "RT");
        CheckTriggerState(newState.Gamepad.LeftTrigger, _state.Gamepad.LeftTrigger, "LT");

        CheckStickState(newState.Gamepad.RightThumbX, _state.Gamepad.RightThumbX, "RSX");
        CheckStickState(newState.Gamepad.RightThumbY, _state.Gamepad.RightThumbY, "RSY");
        CheckStickState(newState.Gamepad.LeftThumbX, _state.Gamepad.LeftThumbX, "LSX");
        CheckStickState(newState.Gamepad.LeftThumbY, _state.Gamepad.LeftThumbY, "LSY");

        _state = newState;
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
            var minValue = Dictionary.sliderSettings[$"Gamepad Minimum {triggerName}"];
            var isPressed = value >= (minValue ?? 0.1);
            InvokeEvent(new GamepadEventArgs { Button = triggerName, IsPressed = isPressed, Value = value });
        }
    }

    private void CheckStickState(short newState, short oldState, string stickName)
    {
        if (newState != oldState)
        {
            InvokeEvent( new GamepadEventArgs { Button = stickName, Value = newState, IsStickEvent = true});
        }
    }

    private void InvokeEvent(GamepadEventArgs args)
    {
        Task.Factory.StartNew(() => ButtonEvent?.Invoke(this, args), CancellationToken.None, TaskCreationOptions.None, _scheduler);
    }

    public class GamepadEventArgs : EventArgs
    {
        private const string Prefix = "GP | ";
        public static bool IsGamepadKey(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
                return false;
            key = GetButtonName(key);
            return key switch
            {
                "A" => true,
                "B" => true,
                "X" => true,
                "Y" => true,
                "LB" => true,
                "RB" => true,
                "RT" => true,
                "LT" => true,
                "RS" => true,
                "LS" => true,
                "RSX" => true,
                "RSY" => true,
                "LSX" => true,
                "LSY" => true,
                _ => false
            };
        }

        public static string GetButtonName(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
                return string.Empty;
            return key[Prefix.Length..];
        }

        public bool IsStickEvent { get; set; }
        public string Button { get; set; }
        public bool? IsPressed { get; set; }
        public float? Value { get; set; }
        public string Code => ToString();
        
        public override string ToString()
        {
            return $"{Prefix}{Button}";
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _pollingTask?.Dispose();
    }
}