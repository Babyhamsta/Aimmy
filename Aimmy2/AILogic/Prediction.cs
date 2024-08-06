using System.Drawing;
using Aimmy2.Types;

namespace Aimmy2.AILogic;

public class Prediction
{
    public bool IntersectsWithCenterOfHeadRelativeRect { get; set; }
    public bool InteractsWithCenterOfFov { get; set; }
    public float Confidence { get; set; }
    public float CenterXTranslated { get; set; }
    public float CenterYTranslated { get; set; }
    public RelativeRect HeadRelativeRect { get; set; } = RelativeRect.Default;
    public RectangleF Rectangle { get; set; }
    public RectangleF TranslatedRectangle { get; set; }
}