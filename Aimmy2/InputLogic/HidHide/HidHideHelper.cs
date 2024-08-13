using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aimmy2.Config;
using Newtonsoft.Json;
using SharpDX.XInput;

namespace Aimmy2.InputLogic.HidHide;

public static class HidHideHelper
{
    private static ConcurrentDictionary<string, HidHideDeviceResult> _deviceCache = new();

    public static void Hide(this Controller controller)
    {
        AddApplicationToHidHide();
        EnableDeviceHiding();
        HideDevice(controller.GetControllerId());
    }

    public static void Show(this Controller controller)
    {
        ShowDevice(controller.GetControllerId());
    }

    private static void AddApplicationToHidHide()
    {
        string appPath = Process.GetCurrentProcess().MainModule.FileName;
        HidHideHelper.ExecuteHidHideCommand($"--app-clean --app-reg \"{appPath}\"");
    }

    private static void EnableDeviceHiding()
    {
        HidHideHelper.ExecuteHidHideCommand("--cloak-on");
    }

    private static void DisableDeviceHiding()
    {
        HidHideHelper.ExecuteHidHideCommand("--cloak-off");
    }

    public static HidHideDeviceResult? DeviceFor(string id)
    {
        if (_deviceCache.TryGetValue(id, out var res))
            return res;
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var d = HidHideHelper.ExecuteHidHideCommand<HidHideDeviceResult[]>("--dev-gaming");
        res = HidHideHelper.FindMatchingDevice(id, d.ToList());
        _deviceCache.TryAdd(id, res);
        return res;
    }

    public static void HideDevice(string deviceId)
    {
        var device = DeviceFor(deviceId);
        if (device == null)
            return;

        device.Devices.ToList().ForEach(d =>
        {
            if (d.Present)
                HidHideHelper.ExecuteHidHideCommand($"--dev-hide \"{d.DeviceInstancePath}\"");
        });
    }

    public static void ShowDevice(string deviceId)
    {
        var device = DeviceFor(deviceId);
        if (device == null)
            return;

        device.Devices.ToList().ForEach(d =>
        {
            if (d.Present)
                HidHideHelper.ExecuteHidHideCommand($"--dev-unhide \"{d.DeviceInstancePath}\"");
        });
    }

    public static HidHideDeviceResult FindMatchingDevice(string controllerId, List<HidHideDeviceResult> hidHideDevices)
    {
        string usbVidPid = ExtractVidPid(controllerId);
        string usbSerial = ExtractSerial(controllerId);

        foreach (var deviceResult in hidHideDevices)
        {
            foreach (var device in deviceResult.Devices)
            {
                // Extract relevant parts from the HID path
                string hidVidPid = ExtractVidPid(device.DeviceInstancePath);
                string hidSerial = ExtractSerial(device.DeviceInstancePath);

                // Compare VID/PID and Serial numbers
                if (string.Equals(usbVidPid, hidVidPid, StringComparison.OrdinalIgnoreCase) 
                    //&& string.Equals(usbSerial, hidSerial, StringComparison.OrdinalIgnoreCase)
                    )
                {
                    return deviceResult; // Return the matching device result
                }
            }
        }

        return null; // No matching device found
    }

    private static string ExtractVidPid(string path)
    {
        var match = Regex.Match(path, @"VID_[0-9A-F]{4}&PID_[0-9A-F]{4}");
        return match.Success ? match.Value : null;
    }

    private static string ExtractSerial(string path)
    {
        // The serial number usually appears after the second backslash
        var parts = path.Split('\\');
        return parts.Length > 2 ? parts[2] : null;
    }

    public static void ExecuteHidHideCommand(string arguments)
    {
        ExecuteHidHideCommand<string>(arguments);
    }

    public static T ExecuteHidHideCommand<T>(string arguments) where T : class
    {
        string hidHidePath = GetHidHidePath();
        if (string.IsNullOrEmpty(hidHidePath))
        {
            Console.WriteLine("HidHideCLI.exe not found.");
            return default;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = hidHidePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            Console.WriteLine(process.ExitCode != 0 ? $"Error on executing HidHide: {error}" : $"Success: {output}");
            try
            {
                if (typeof(T) == typeof(string))
                {
                    return output as T;
                }

                return JsonConvert.DeserializeObject<T>(output);
            }
            catch
            {

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception in HidHideCLI: " + ex.Message);
        }

        return default;
    }

    public static string GetHidHidePath()
    {
        string hidHidePath = AppConfig.Current.FileLocationState.HidHidePath;
        if (!File.Exists(hidHidePath))
            hidHidePath = GetHidHideDefaultPath();
        return File.Exists(hidHidePath) ? hidHidePath : null;
    }

    public static string GetHidHideDefaultPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe");
    }
}