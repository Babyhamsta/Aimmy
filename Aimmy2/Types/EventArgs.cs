namespace Aimmy2.Types;

public class EventArgs<T> : EventArgs
{
    public EventArgs(T value)
    {
        Value = value;
    }
    public T Value { get; set; }
}