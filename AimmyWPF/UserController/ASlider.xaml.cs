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
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        static MainWindow MainWin = new MainWindow();

        public ASlider(MainWindow MW, string Text, string NotifierText, string Info, double ButtonSteps)
        {
            InitializeComponent();
            Title.Content = Text;

            Slider.ValueChanged += (s, e) =>
            {
                if (AdjustNotifier != null)
                    AdjustNotifier.Content = $"{Slider.Value.ToString()} {NotifierText}";

                // Added by Nori
                Slider.Value = Math.Round(Slider.Value, 2);
            };

            // Added by Nori
            SubtractOne.Click += (s, e) => Slider.Value = Slider.Value - ButtonSteps;
            AddOne.Click += (s, e) => Slider.Value = Slider.Value + ButtonSteps;

            QuestionButton.Click += (s, e) => {
                MainWin.ActivateMoreInfo(Info);
            };

            MainWin = MW;
        }
    }
}
