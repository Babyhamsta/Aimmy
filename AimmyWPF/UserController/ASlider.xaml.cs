using System;
using System.Windows.Controls;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        private static MainWindow MainWin = new MainWindow();

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

            QuestionButton.Click += (s, e) =>
            {
                MainWin.ActivateMoreInfo(Info);
            };

            MainWin = MW;
        }
    }
}