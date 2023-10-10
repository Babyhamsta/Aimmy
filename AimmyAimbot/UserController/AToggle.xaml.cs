using AimmyAimbot.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AimmyAimbot.UserController
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : UserControl
    {
        public AToggle(string Text)
        {
            InitializeComponent();
            Title.Content = Text;
        }

        public void EnableSwitch()
        {
            G1.Color = Color.FromRgb(156, 207, 216);
            G2.Color = Color.FromRgb(86, 148, 159);

            GG1.Color = Color.FromRgb(156, 207, 216);
            GG2.Color = Color.FromRgb(86, 148, 159);
        }
        public void DisableSwitch()
        {
            G1.Color = Color.FromRgb(235, 111, 146);
            G2.Color = Color.FromRgb(180, 99, 122);

            GG1.Color = Color.FromRgb(235, 111, 146);
            GG2.Color = Color.FromRgb(180, 99, 122);
        }
    }
}
