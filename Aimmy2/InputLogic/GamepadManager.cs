namespace Aimmy2.InputLogic;

public static class GamepadManager
{
    public static bool IsInitialized { get; private set; }
    public static GamepadReader GamepadReader { get; private set; }
    public static GamepadSender GamepadSender { get; private set; }

    public static void Init()
    {
        GamepadReader = new GamepadReader();
        GamepadSender = new GamepadSender();
        IsInitialized = true;
    }
}