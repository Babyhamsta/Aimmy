namespace Aimmy2.AILogic;

public static class Normalizer
{
    public static Func<double[], double[], double> SquaredDouble = (x, y) => x.Select((t, i) => (t - y[i]) * (t - y[i])).Sum();
}