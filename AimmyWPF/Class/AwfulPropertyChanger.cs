using System;

namespace AimmyWPF.Class
{
    public static class AwfulPropertyChanger
    {
        // Reference: https://www.codeproject.com/Questions/5363839/How-to-change-property-of-element-from-outside-of
        // not a good coder nori

        // FOV Color
        public static void PostColor(System.Windows.Media.Color newcolor) => ReceiveColor?.Invoke(newcolor);

        public static Action<System.Windows.Media.Color> ReceiveColor { private get; set; } = null;

        // FOV Size
        public static void PostNewFOVSize() => ReceiveFOVSize?.Invoke();

        public static Action ReceiveFOVSize { private get; set; } = null;

        // FOV Following
        public static void PostTravellingFOV(bool TravellingState) => ReceiveTravellingFOV?.Invoke(TravellingState);

        public static Action<bool> ReceiveTravellingFOV { private get; set; } = null;

        // PDW Size
        public static void PostPDWSize(int newint) => ReceivePDWSize?.Invoke(newint);

        public static Action<int> ReceivePDWSize { private get; set; } = null;

        // PDW Corner Radius
        public static void PostPDWCornerRadius(int newint) => ReceivePDWCornerRadius?.Invoke(newint);

        public static Action<int> ReceivePDWCornerRadius { private get; set; } = null;

        // PDW Border Thickness
        public static void PostPDWBorderThickness(int newint) => ReceivePDWBorderThickness?.Invoke(newint);

        public static Action<int> ReceivePDWBorderThickness { private get; set; } = null;

        // PDW Border Thickness
        public static void PostPDWOpacity(double newdouble) => ReceivePDWOpacity?.Invoke(newdouble);

        public static Action<double> ReceivePDWOpacity { private get; set; } = null;
    }
}