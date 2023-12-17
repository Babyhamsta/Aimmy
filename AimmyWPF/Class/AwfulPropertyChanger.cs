using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AimmyWPF.Class
{
    public static class AwfulPropertyChanger
    {
        // Reference: https://www.codeproject.com/Questions/5363839/How-to-change-property-of-element-from-outside-of
        // not a good coder nori

        public static void PostColor(System.Windows.Media.Color newcolor) => ReceiveColor?.Invoke(newcolor);
        public static Action<System.Windows.Media.Color> ReceiveColor { private get; set; } = null;
        public static void PostNewFOVSize() => ReceiveFOVSize?.Invoke();
        public static Action ReceiveFOVSize { private get; set; } = null;
    }
}
