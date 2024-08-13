namespace Aimmy2.InputLogic.Contracts;

public class GamepadEventArgs : EventArgs
{
    private const string Prefix = "GP | ";
    public static bool IsGamepadKey(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
            return false;
        key = GetButtonName(key);
        return key switch
        {
            "A" => true,
            "B" => true,
            "X" => true,
            "Y" => true,
            "LB" => true,
            "RB" => true,
            "RT" => true,
            "LT" => true,
            "RS" => true,
            "LS" => true,
            "RSX" => true,
            "RSY" => true,
            "LSX" => true,
            "LSY" => true,
            _ => false
        };
    }

    public static string GetButtonName(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix))
            return string.Empty;
        return key[Prefix.Length..];
    }

    public bool IsStickEvent { get; set; }
    public string Button { get; set; }
    public bool? IsPressed { get; set; }
    public float? Value { get; set; }
    public string Code => ToString();

    public override string ToString()
    {
        return $"{Prefix}{Button}";
    }
}