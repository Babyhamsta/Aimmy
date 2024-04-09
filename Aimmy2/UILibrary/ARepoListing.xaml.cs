using Aimmy2.Class;
using System.Windows;
using System.Windows.Controls;
using Visuality;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ARepoListing.xaml
    /// </summary>
    public partial class ARepoListing : UserControl
    {
        public ARepoListing(string name, bool config)
        {
            InitializeComponent();

            Title.Content = name;

            if (config) { Label.Content = "Configs"; }

            RemovalButton.Click += (s, e) =>
            {
                if (Parent is StackPanel stackPanel && Dictionary.repoList.ContainsKey(name))
                {
                    Application.Current.Dispatcher.Invoke(() => stackPanel.Children.Remove(this));

                    Dictionary.repoList.Remove(name);

                    RepoManager.UpdateStoreMenu(config);
                }
                //else
                //{
                //    Debug.WriteLine("Failed to remove repo.");
                //    Debug.WriteLine("Parent is not a StackPanel or the repo does not exist.");
                //    Debug.WriteLine(Dictionary.repoList.Keys);
                //}
            };
        }
    }
}