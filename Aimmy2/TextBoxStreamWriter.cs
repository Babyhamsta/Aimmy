using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Aimmy2;

public class TextBoxStreamWriter : TextWriter
{
    private readonly TextBox _output;

    public TextBoxStreamWriter(TextBox output)
    {
        _output = output;
    }

    public override void Write(char value)
    {
        //base.Write(value);
        Write(value.ToString());
    }

    public override void Write(string value)
    {
       // base.Write(value);
        _output.Dispatcher.BeginInvoke(new Action(() =>
        {
            // Add text to the beginning of the TextBox
            _output.Text = value + _output.Text;
        }));
    }

    public override Encoding Encoding => Encoding.UTF8;
}