using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Aimmy2.AILogic;
using Aimmy2.Config;
using Class;

namespace Aimmy2.Other
{
    internal static class PredictionDrawer
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static void DrawPredictions(IEnumerable<Prediction> predictions, float scaleX, float scaleY)
        {
            IntPtr desktopDC = GetDC(IntPtr.Zero);
            using (Graphics graphics = Graphics.FromHdc(desktopDC))
            {
                foreach (var prediction in predictions)
                {
                    DrawPrediction(graphics, prediction, scaleX, scaleY);
                }
            }
            ReleaseDC(IntPtr.Zero, desktopDC);
        }

        private static void DrawPrediction(Graphics graphics, Prediction prediction, float scaleX, float scaleY)
        {
            // Skalierung und Positionierung basierend auf der Bildschirmauflösung und den AI-Vorhersagen
            var rect = new RectangleF(
                prediction.Rectangle.X * scaleX,
                prediction.Rectangle.Y * scaleY,
                prediction.Rectangle.Width * scaleX,
                prediction.Rectangle.Height * scaleY
            );

            // Farbe und Zeichenstift für das Zeichnen des Rechtecks und der Texte
            var color = AppConfig.Current.ColorState.DetectedPlayerColor;
            var pen = new Pen(Color.FromArgb((int)(255 * AppConfig.Current.SliderSettings.Opacity), color.R, color.G, color.B), (float)AppConfig.Current.SliderSettings.BorderThickness);
            var font = new Font("Consolas", AppConfig.Current.SliderSettings.AIConfidenceFontSize);
            var brush = new SolidBrush(Color.FromArgb((int)(255 * AppConfig.Current.SliderSettings.Opacity), color.R, color.G, color.B));

            // Abrundung der Ecken des Rechtecks
            var graphicsPath = new System.Drawing.Drawing2D.GraphicsPath();
            float cornerRadius = Math.Max((float)AppConfig.Current.SliderSettings.CornerRadius, (float)0.1);

            graphicsPath.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
            graphicsPath.AddArc(rect.X + rect.Width - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
            graphicsPath.AddArc(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
            graphicsPath.AddArc(rect.X, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            graphicsPath.CloseFigure();

            // Zeichnen des Rechtecks
            graphics.DrawPath(pen, graphicsPath);

            // Zeichnen des Vertrauenswerts
            if (AppConfig.Current.ToggleState.ShowAIConfidence)
            {
                var confidenceText = $"{Math.Round((prediction.Confidence * 100), 2)}%";
                var textSize = graphics.MeasureString(confidenceText, font);
                graphics.DrawString(confidenceText, font, brush, rect.X + (rect.Width - textSize.Width) / 2, rect.Y - textSize.Height - 2);
            }

            // Zeichnen der Tracer-Linie
            if (AppConfig.Current.ToggleState.ShowTracers)
            {
                var centerX = rect.X + rect.Width / 2;
                var bottomY = rect.Y + rect.Height;
                graphics.DrawLine(pen, new PointF(WinAPICaller.ScreenWidth / 2, WinAPICaller.ScreenHeight), new PointF(centerX, bottomY));
            }

            // Zeichnen des Head Area
            if (AppConfig.Current.ToggleState.ShowTriggerHeadArea)
            {
                var headRelativeRect = prediction.HeadRelativeRect;
                float headAreaWidth = rect.Width * headRelativeRect.WidthPercentage;
                float headAreaHeight = rect.Height * headRelativeRect.HeightPercentage;
                float headAreaLeft = rect.X + rect.Width * headRelativeRect.LeftMarginPercentage;
                float headAreaTop = rect.Y + rect.Height * headRelativeRect.TopMarginPercentage;

                var headAreaRect = new RectangleF(headAreaLeft, headAreaTop, headAreaWidth, headAreaHeight);
                var headPen = new Pen(Color.Green, 2);
                graphics.DrawRectangle(headPen, headAreaRect.X, headAreaRect.Y, headAreaRect.Width, headAreaRect.Height);
                headPen.Dispose();
            }

            // Ressourcen freigeben
            pen.Dispose();
            font.Dispose();
            brush.Dispose();
            graphicsPath.Dispose();
        }
    }
}
