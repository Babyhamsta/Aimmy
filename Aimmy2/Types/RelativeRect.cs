using System.Globalization;

namespace Aimmy2.Types;

public struct RelativeRect : ISpanParsable<RelativeRect>
{
    public float WidthPercentage { get; set; }
    public float HeightPercentage { get; set; }
    public float LeftMarginPercentage { get; set; }
    public float TopMarginPercentage { get; set; }

    public static RelativeRect Default => new RelativeRect(0.5f, 0.29f, 0.25f, 0.05f);

    public RelativeRect(float widthPercentage, float heightPercentage, float leftMarginPercentage, float topMarginPercentage)
    {
        WidthPercentage = widthPercentage;
        HeightPercentage = heightPercentage;
        LeftMarginPercentage = leftMarginPercentage;
        TopMarginPercentage = topMarginPercentage;
    }

    public override string ToString()
    {
        return ToString(CultureInfo.InvariantCulture);
    }

    public string ToString(IFormatProvider? provider)
    {
        return $"{WidthPercentage.ToString("F2", provider)}|{HeightPercentage.ToString("F2", provider)}|{LeftMarginPercentage.ToString("F2", provider)}|{TopMarginPercentage.ToString("F2", provider)}";
    }

    public static RelativeRect Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        var parts = s.ToString().Split('|');
        if (parts.Length != 4)
        {
            throw new FormatException("Input string was not in the correct format.");
        }

        return new RelativeRect(
            float.Parse(parts[0], provider),
            float.Parse(parts[1], provider),
            float.Parse(parts[2], provider),
            float.Parse(parts[3], provider)
        );
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out RelativeRect result)
    {
        var parts = s.ToString().Split('|');
        if (parts.Length == 4 &&
            float.TryParse(parts[0], NumberStyles.Float, provider, out float width) &&
            float.TryParse(parts[1], NumberStyles.Float, provider, out float height) &&
            float.TryParse(parts[2], NumberStyles.Float, provider, out float leftMargin) &&
            float.TryParse(parts[3], NumberStyles.Float, provider, out float topMargin))
        {
            result = new RelativeRect(width, height, leftMargin, topMargin);
            return true;
        }

        result = default;
        return false;
    }

    public static RelativeRect Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse(string s, IFormatProvider? provider, out RelativeRect result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static RelativeRect Parse(ReadOnlySpan<char> s)
    {
        return Parse(s, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out RelativeRect result)
    {
        return TryParse(s, CultureInfo.InvariantCulture, out result);
    }

    public static RelativeRect Parse(string s)
    {
        return Parse(s, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string s, out RelativeRect result)
    {
        return TryParse(s, CultureInfo.InvariantCulture, out result);
    }

    public static RelativeRect ParseOrDefault(string relativeRect)
    {
        return !string.IsNullOrWhiteSpace(relativeRect) && TryParse(relativeRect, out var result) ? result : Default;
    }
}