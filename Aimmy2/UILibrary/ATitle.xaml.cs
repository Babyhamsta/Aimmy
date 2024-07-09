using Aimmy2.Class;
using Aimmy2.Config;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ATitle.xaml
    /// </summary>
    public partial class ATitle : System.Windows.Controls.UserControl
    {
        public ATitle(string Text, bool MinimizableMenu = false)
        {
            InitializeComponent();

            LabelTitle.Content = Text;

            if (MinimizableMenu)
            {
                Minimize.Visibility = System.Windows.Visibility.Visible;
                var b = AppConfig.Current.MinimizeState[Text];
                switch (bool.Parse(b?.ToString()))
                {
                    case false:
                        Minimize.Content = "\xE921";
                        break;

                    case true:
                        Minimize.Content = "\xE710";
                        break;
                }
            }

            Minimize.Click += (s, e) =>
            {
                var b = AppConfig.Current.MinimizeState[Text];
                //Debug.WriteLine(Minimize.Content);
                var minimized = bool.Parse(b?.ToString());
                switch (minimized)
                {
                    case false:
                        Minimize.Content = "\xE710";
                        break;

                    case true:
                        Minimize.Content = "\xE921";
                        break;
                }

                AppConfig.Current.MinimizeState[Text] = !minimized;
            };
        }
    }
}