using System.Windows;
using System.Windows.Markup;
using System.ComponentModel;

namespace Aimmy2.Resources
{
    public class LocExtension : MarkupExtension
    {
        private static readonly bool _isInDesignMode;

        public string Key { get; }

        static LocExtension()
        {
            _isInDesignMode = DesignerProperties.GetIsInDesignMode(
                new DependencyObject());
        }

        public LocExtension(string key)
        {
            Key = key ?? string.Empty;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            if (_isInDesignMode)
                return Key;

            try
            {
                return LocalizationManager.GetString(Key);
            }
            catch
            {
                return Key;
            }
        }
    }
}
