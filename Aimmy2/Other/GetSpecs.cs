using Aimmy2.Resources;
using System.Management;
using Visuality;
using Vortice.DXGI;

namespace Aimmy2.Class
{
    internal class GetSpecs
    {
        // Reference: https://www.youtube.com/watch?v=rou471Evuzc
        // Nori
        public static string? GetSpecification(string HardwareClass, string Syntax)
        {
            try
            {
                if (HardwareClass == "Win32_VideoController" && Syntax == "Name")
                {
                    return GetActiveGpuName();
                }
                ManagementObjectSearcher SpecsSearch = new("root\\CIMV2", "SELECT * FROM " + HardwareClass);
                foreach (ManagementObject MJ in SpecsSearch.Get().Cast<ManagementObject>())
                {
                    return Convert.ToString(MJ[Syntax])?.Trim();
                }
                return LocalizationManager.GetString("Common_NotFound");
            }
            catch (Exception e)
            {
                new NoticeBar(LocalizationManager.GetString("Msg_GeneralError", e.Message), 10000).Show();
                return LocalizationManager.GetString("Common_NotFound");
            }
        }
        private static string GetActiveGpuName()
        {
            try
            {
                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

                for (uint i = 0; factory.EnumAdapters1(i, out IDXGIAdapter1 adapter).Success; i++)
                {
                    AdapterDescription1 desc = adapter.Description1;
                    if ((desc.Flags & AdapterFlags.Software) == 0)
                    {
                        return desc.Description.Trim();
                    }
                }

                return LocalizationManager.GetString("Common_GpuNotFound");
            }
            catch (Exception e)
            {
                new NoticeBar(LocalizationManager.GetString("Msg_DXGIError", e.Message, e.HResult), 10000).Show();
                return LocalizationManager.GetString("Common_GpuError");
            }
        }
    }
}

/*
We can try this to fix the issue for users in different languages
public static string? GetSpecification(string HardwareClass, string Syntax)
{
    try
    {
        if (HardwareClass == "Win32_VideoController" && Syntax == "Name")
        {
            return GetActiveGpuName();
        }
        ManagementObjectSearcher SpecsSearch = new($"SELECT * FROM {HardwareClass}");
        foreach (ManagementObject MJ in SpecsSearch.Get().Cast<ManagementObject>())
        {
            try
            {
                return Convert.ToString(MJ[Syntax])?.Trim();
            }
            catch (ManagementException)
            {
                foreach (var prop in MJ.Properties)
                {
                    if (string.Equals(prop.Name, Syntax, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToString(prop.Value)?.Trim();
                    }
                }
            }
        }
        return "Not Found";
    }
    catch (Exception e)
    {
        new NoticeBar(e.Message, 10000).Show();
        return "Not Found";
    }
}
*/