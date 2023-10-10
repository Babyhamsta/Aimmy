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
    /// Interaction logic for ALabel.xaml
    /// </summary>
    public partial class ALabel : UserControl
    {
        public ALabel(string Text)
        {
            InitializeComponent();
            Title.Content = Text;
        }
    }
}
