using System.ComponentModel;

namespace Aimmy2.Config;

public enum OverlayDrawingMethod
{
    [Description("WPF Overlay Window")]
    WpfWindow,
    [Description("Desktop Graphic Context Draw")]
    DesktopDC
}