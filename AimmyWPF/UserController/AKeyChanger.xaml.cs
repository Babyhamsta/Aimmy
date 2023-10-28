using AimmyWPF.Class;
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

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : UserControl
    {
        public AKeyChanger(string Text, string DefaultContent)
        {
            InitializeComponent();
            Title.Content = Text;
            KeyNotifier.Content = DefaultContent;

            //MainWin.ActivateMoreInfo();
        }
    }
}
