
using System.Windows;

namespace WpfAutoUpdater
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        public void OpenUpdatedViewWindow()
        {
            var view = new View { Owner = this };
            view.Show(); // or ShowDialog();
        }
    }
}
