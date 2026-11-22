using System.Drawing;

namespace Aimmy2.AILogic
{
    public class Prediction
    {
        public RectangleF Rectangle { get; set; }

        internal RectangleF ScreenBounds => new(ScreenCenterX - Rectangle.Width / 2,
            ScreenCenterY - Rectangle.Height / 2, Rectangle.Width, Rectangle.Height);

        public float Confidence { get; set; }
        public int ClassId { get; set; } = 0;
        public string ClassName { get; set; } = "Enemy";
        public float CenterXTranslated { get; set; }
        public float CenterYTranslated { get; set; }
        public float ScreenCenterX { get; set; }
        public float ScreenCenterY { get; set; }
    }
}