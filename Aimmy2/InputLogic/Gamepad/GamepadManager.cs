using System.Diagnostics;
using System.IO;
using Aimmy2.Config;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.InputLogic.Gamepad.Interaction;
using Aimmy2.InputLogic.HidHide;
using Aimmy2.Models;

namespace Aimmy2.InputLogic;

public static class GamepadManager
{
    private static bool _controllerHidden;
    public static bool CanRead { get; private set; }
    public static IGamepadReader? GamepadReader { get; private set; }
    public static IGamepadSender? GamepadSender { get; private set; }

    public static bool CanSend => GamepadSender?.CanWork ?? false;

    public static void Init()
    {
        Dispose();
        if (GamepadReader == null)
        {
            GamepadReader = new GamepadReader();
            GamepadReader.Controller.GetControllerId(); // Needs to be called before virtual one is created
        }

        try
        {
            GamepadSender = CreateSender();
            GamepadSender?.SyncWith(GamepadReader.Controller);
            _controllerHidden = GamepadSender != null && AppConfig.Current.ToggleState.AutoHideController;
            if (_controllerHidden)
                GamepadReader.Controller.Hide();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            GamepadSender = null;
            throw e;
        }
        finally
        {
            CanRead = true;
        }
    }

    private static IGamepadSender? CreateSender()
    {
        return AppConfig.Current.DropdownState.GamepadSendMode switch
        {
            GamepadSendMode.ViGEm => new GamepadSenderViGEm(),
            GamepadSendMode.VJoy => new GamepadSenderVJoy(),
            GamepadSendMode.XInputHook => CreateXInputHook(),
            _ => null
        };
    }

    private static IGamepadSender CreateXInputHook()
    {
        var process = ProcessModel.FindProcessByTitle(AppConfig.Current.DropdownState.GamepadProcess);
        if (process == null)
            throw new Exception("Process not found");
        var xInputEmuProcess = Process.GetProcesses().FirstOrDefault(p =>
        {
            try
            {
                return Path.GetFileName(p.MainModule.FileName) == "XInputEmu.exe";
            }
            catch (Exception e)
            {
                return false;
            }
        });
        if (xInputEmuProcess != null)
            xInputEmuProcess.Kill();
        var fileName = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Resources", "XInputEmu", "XInputEmu.exe");
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = $"{process.Id}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(fileName)
        };
        Process.Start(startInfo);
        return new GamepadSenderXInputEmu();
    }

    public static void Dispose()
    {
        if (_controllerHidden)
            GamepadReader?.Controller.Show();
        // GamepadReader?.Dispose();
        GamepadSender?.Dispose();
        CanRead = false;
    }

}