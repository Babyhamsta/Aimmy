using System.Windows;
using System.Windows.Controls;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.Extensions;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ATitle.xaml
    /// </summary>
    public partial class ATitle : System.Windows.Controls.UserControl
    {
        const string maximizeIcon = "\xE710";
        const string minimizeContent = "\xE921";

        public ATitle(string text, bool minimizable = false)
        {
            InitializeComponent();

            LabelTitle.Content = text;


            if (minimizable)
            {
                Minimize.Visibility = System.Windows.Visibility.Visible;
                this.InitWith(async title =>
                {
                    await Task.Delay(200);
                    SetMenuVisibility(Parent as StackPanel, !AppConfig.Current.MinimizeState.IsMinimized(text));
                });
                Minimize.Click += (s, e) =>
                {
                    var minimized = AppConfig.Current.MinimizeState.IsMinimized(text);
                    SetMenuVisibility(Parent as StackPanel, minimized);
                    AppConfig.Current.MinimizeState.SetMinimized(text, !minimized);
                };
            }
            
        }

        private void SetMenuVisibility(StackPanel? panel, bool isVisible)
        {
            if (panel == null)
                return;
            Minimize.Content = isVisible ? minimizeContent : maximizeIcon;
            foreach (UIElement child in panel.Children)
                if (!(child is ATitle || child is ASpacer || child is ARectangleBottom))
                    child.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                else
                    child.Visibility = Visibility.Visible;
        }
    }
}