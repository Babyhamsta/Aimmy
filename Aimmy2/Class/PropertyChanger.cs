namespace Aimmy2.Class
{
    public static class PropertyChanger
    {
        // Reference: https://www.codeproject.com/Questions/5363839/How-to-change-property-of-element-from-outside-of
        // not a good coder nori

        // FOV Size
        public static void PostNewFOVSize(double newsize) => ReceiveFOVSize?.Invoke(newsize);

        public static Action<double>? ReceiveFOVSize { private get; set; } = null;

        // FOV Color
        public static void PostColor(System.Windows.Media.Color newcolor) => ReceiveColor?.Invoke(newcolor);

        public static Action<System.Windows.Media.Color>? ReceiveColor { private get; set; } = null;

        // Detected Player Color
        public static void PostDPColor(System.Windows.Media.Color newcolor) => ReceiveDPColor?.Invoke(newcolor);

        public static Action<System.Windows.Media.Color>? ReceiveDPColor { private get; set; } = null;

        // Detected Player Font Size
        public static void PostDPFontSize(int newint) => ReceiveDPFontSize?.Invoke(newint);

        public static Action<int>? ReceiveDPFontSize { private get; set; } = null;

        // DPWindow Corner Radius
        public static void PostDPWCornerRadius(int newint) => ReceiveDPWCornerRadius?.Invoke(newint);

        public static Action<int>? ReceiveDPWCornerRadius { private get; set; } = null;

        // DPWindow Border Thickness
        public static void PostDPWBorderThickness(double newdouble) => ReceiveDPWBorderThickness?.Invoke(newdouble);

        public static Action<double>? ReceiveDPWBorderThickness { private get; set; } = null;

        // DPWindow Opacity
        public static void PostDPWOpacity(double newdouble) => ReceiveDPWOpacity?.Invoke(newdouble);

        public static Action<double>? ReceiveDPWOpacity { private get; set; } = null;

        // Post New Config
        public static void PostNewConfig(string path, bool load) => ReceiveNewConfig?.Invoke(path, load);

        public static Action<string, bool>? ReceiveNewConfig { private get; set; } = null;
    }
}