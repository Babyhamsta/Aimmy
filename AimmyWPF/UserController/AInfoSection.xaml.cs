using AimmyWPF.Class;
using SecondaryWindows;
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
    /// Interaction logic for AInfoSection.xaml
    /// </summary>
    public partial class AInfoSection : UserControl
    {
        public AInfoSection()
        {
            InitializeComponent();
            //Title.Content = Text;
            CheckForUpdates.Click += (s, e) => {
                new NoticeBar("This feature has not been implemented yet.").Show();
            };
        }
    }
}
