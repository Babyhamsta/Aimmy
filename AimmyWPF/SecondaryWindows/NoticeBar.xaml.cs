using AimmyWPF.Class;
using MaterialDesignThemes.Wpf.Transitions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SecondaryWindows
{
    /// <summary>
    /// Interaction logic for NoticeBar.xaml
    /// </summary>
    public partial class NoticeBar : Window
    {
        public NoticeBar(string Text)
        {
            InitializeComponent();
            Notice.Opacity = 0;
            ContentText.Content = Text;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Animator.Fade(Notice);
            await Task.Delay(5000);
            Animator.FadeOut(Notice);
            await Task.Delay(1000);
            this.Close();
        }
    }
}
