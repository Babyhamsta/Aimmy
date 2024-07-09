using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.Config;
using Nextended.Core;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Aimmy2.Extensions;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AColorChanger.xaml
    /// </summary>
    public partial class AColorChanger : INotifyPropertyChanged
    {
        private Color _color;
        private string _title;

        public Color Color
        {
            get => _color;
            set => SetField(ref _color, value);
        }

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public AColorChanger()
        {
            InitializeComponent();
            DataContext = this;
        }

        public AColorChanger(string title) : this()
        {
            Title = title;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AColorChanger BindTo(Expression<Func<Color>> fn)
        {
            var memberExpression = fn.GetMemberExpression();
            var propertyInfo = (PropertyInfo)memberExpression.Member;
            var owner = memberExpression.GetOwnerAs<INotifyPropertyChanged>();

            Color = fn.Compile()();

            owner.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyInfo.Name)
                {
                    Color = fn.Compile()();
                    ColorChangingBorder.Background = new SolidColorBrush(Color);
                }
            };

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Color))
                    propertyInfo.SetValue(owner, Color);
            };

            return this;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void ChangeColorClick(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Color = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
            }
        }
    }
}