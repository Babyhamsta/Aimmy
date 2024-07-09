using Nextended.Core;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Channels;
using System.Windows.Controls;
using System.Windows.Input;
using Aimmy2.Extensions;
using System.Numerics;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        public ASlider(string Text, string NotifierText, double ButtonSteps)
        {
            InitializeComponent();

            SliderTitle.Content = Text;

            Slider.ValueChanged += (s, e) =>
            {
                AdjustNotifier.Content = $"{Slider.Value:F2} {NotifierText}";
            };

            SubtractOne.Click += (s, e) => UpdateSliderValue(-ButtonSteps);
            AddOne.Click += (s, e) => UpdateSliderValue(ButtonSteps);
        }

        public ASlider BindTo<T>(Expression<Func<T>> fn) where T : struct, INumber<T>
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Slider.Value = Convert.ToDouble(fn.Compile()());

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Slider.Value = Convert.ToDouble(fn.Compile()());
                }
            };

            Slider.ValueChanged += (s, e) =>
            {
                propertyInfo.SetValue(owner, T.CreateChecked(e.NewValue));
            };

            return this;
        }


        private void UpdateSliderValue(double change)
        {
            Slider.Value = Math.Round(Slider.Value + change, 2);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void Slider_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            System.Windows.MessageBox.Show($"{Slider.Value:F2}");
        }
    }
}