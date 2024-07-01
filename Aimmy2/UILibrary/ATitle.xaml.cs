using Aimmy2.Class;

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
                switch (Dictionary.minimizeState[Text])
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
                //Debug.WriteLine(Minimize.Content);
                switch (Dictionary.minimizeState[Text])
                {
                    case false:
                        Minimize.Content = "\xE710";
                        break;

                    case true:
                        Minimize.Content = "\xE921";
                        break;
                }

                Dictionary.minimizeState[Text] = !Dictionary.minimizeState[Text];
            };
        }
    }
}