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
                foreach (ManagementObject MJ in SpecsSearch.Get().Cast<ManagementObject>())
                {
                    return Convert.ToString(MJ[Syntax])?.Trim();
                }
                return "Not Found";
            }
            catch (Exception e)
            {
                new NoticeBar(e.Message, 10000).Show();
                return "Not Found";
            }
        }
    }
}