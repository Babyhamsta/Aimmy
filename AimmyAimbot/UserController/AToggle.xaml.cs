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
using System.Windows.Media.Animation;
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

        // Reference: https://stackoverflow.com/questions/34815532/start-storyboard-from-c-sharp-code

        public void EnableSwitch()
        {
            Storyboard Animation = (Storyboard)TryFindResource("EnableSwitch");
            Animation.Begin();
        }
        public void DisableSwitch()
        {
            Storyboard Animation = (Storyboard)TryFindResource("DisableSwitch");
            Animation.Begin();
        }
    }
}
