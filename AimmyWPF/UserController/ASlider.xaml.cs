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
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        public ASlider(string Text, string NotifierText)
        {
            InitializeComponent();
            Title.Content = Text;

            Slider.ValueChanged += (s, e) =>
            {
                if (AdjustNotifier != null)
                    AdjustNotifier.Content = $"{Slider.Value.ToString()} {NotifierText}";
            };
        }

        private void AddOne_Click(object sender, RoutedEventArgs e)
        {
            Slider.Value = Slider.Value + 1;
        }

        private void SubtractOne_Click(object sender, RoutedEventArgs e)
        {
            Slider.Value = Slider.Value - 1;
        }
    }
}
