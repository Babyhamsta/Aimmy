using Aimmy2.Class;

namespace Aimmy2.UILibrary
{
    public partial class ATitle : System.Windows.Controls.UserControl
    {
        public ATitle(string Text, bool MinimizableMenu = false)
        {
            InitializeComponent();

            LabelTitle.Content = Text;

            if (MinimizableMenu)
            {
                Minimize.Visibility = System.Windows.Visibility.Visible;
                bool isMinimized = Dictionary.minimizeState.TryGetValue(Text, out var val) && val;
                Minimize.Content = isMinimized ? "\xE710" : "\xE921";
            }

            Minimize.Click += (s, e) =>
            {
                bool isMinimized = Dictionary.minimizeState.TryGetValue(Text, out var val) && val;
                Minimize.Content = isMinimized ? "\xE921" : "\xE710";

                Dictionary.minimizeState[Text] = !isMinimized;
            };
        }
    }
}
