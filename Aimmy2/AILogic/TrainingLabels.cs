namespace Aimmy2.AILogic
{
    internal static class TrainingLabels
    {
        internal static void Write(string path, IReadOnlyList<Prediction> boxes, int width, int height) =>
            System.IO.File.WriteAllLines(path, Format(boxes, width, height));

        private static IEnumerable<string> Format(IReadOnlyList<Prediction> boxes, int width, int height)
        {
            foreach (var box in boxes)
            {
                var r = box.Rectangle;
                yield return FormattableString.Invariant($"{box.ClassId} {(r.X + r.Width / 2) / width:R} {(r.Y + r.Height / 2) / height:R} {r.Width / width:R} {r.Height / height:R}");
            }
        }
    }
}