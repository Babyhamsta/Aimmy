namespace Aimmy2.InputLogic.HidHide;


public class HidHideDeviceResult
{
    public string FriendlyName { get; set; }
    public Device[] Devices { get; set; }
}

public class Device
{
    public bool Present { get; set; }
    public bool GamingDevice { get; set; }
    public string SymbolicLink { get; set; }
    public string Vendor { get; set; }
    public string Product { get; set; }
    public string SerialNumber { get; set; }
    public string Usage { get; set; }
    public string Description { get; set; }
    public string DeviceInstancePath { get; set; }
    public string BaseContainerDeviceInstancePath { get; set; }
    public string BaseContainerClassGuid { get; set; }
    public int BaseContainerDeviceCount { get; set; }

    public bool IsFor(string id)
    {
        return Check(id) || Check(id.Replace(@"\", @"\\"));
    }

    private bool Check(string id)
    {
        return DeviceInstancePath.Contains(id, StringComparison.InvariantCultureIgnoreCase) ||
               BaseContainerClassGuid.Contains(id, StringComparison.InvariantCultureIgnoreCase) ||
               BaseContainerDeviceInstancePath.Contains(id, StringComparison.InvariantCultureIgnoreCase) ||
               SymbolicLink.Contains(id, StringComparison.InvariantCultureIgnoreCase);
    }
}
