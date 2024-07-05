using System.Management;
using Visuality;

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
                ManagementObjectSearcher SpecsSearch = new("root\\CIMV2", "SELECT * FROM " + HardwareClass);
                var res = SpecsSearch.Get().Cast<ManagementObject>().Select(mj => Convert.ToString(mj[Syntax])?.Trim()).ToList();
                return res.Any() ? string.Join(", ", res) : "Not Found";
            }
            catch (Exception e)
            {
                new NoticeBar(e.Message, 10000).Show();
                return "Not Found";
            }
        }
    }
}