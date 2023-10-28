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
    /// Interaction logic for AButton.xaml
    /// </summary>
    public partial class AButton : UserControl
    {
        static MainWindow MainWin = new MainWindow();

        public AButton(MainWindow MW, string Text, string Info)
        {
            InitializeComponent();
            Title.Content = Text;

            MainWin = MW;

            QuestionButton.Click += (s, e) => {
                MainWin.ActivateMoreInfo(Info);
            };
        }
    }
}
