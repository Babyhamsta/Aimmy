using System.Management;

namespace Aimmy2.Class
{
    class GetSpecs
    {
        // Reference: https://www.youtube.com/watch?v=rou471Evuzc
        // Nori
        public static string? GetSpecification(string HardwareClass, string Syntax)
        {
            ManagementObjectSearcher SpecsSearch = new("root\\CIMV2", "SELECT * FROM " + HardwareClass);
            foreach (ManagementObject MJ in SpecsSearch.Get().Cast<ManagementObject>())
            {
                return Convert.ToString(MJ[Syntax])?.Trim();
            }
            return "Not Found";
        }
    }
}
