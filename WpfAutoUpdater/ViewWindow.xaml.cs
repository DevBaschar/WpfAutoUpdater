using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfAutoUpdater;

public partial class ViewWindow : Window
{
    public string CurrentVersion { get; set; }
    public ViewWindow(string currentVersion)
    {
        InitializeComponent();
        CurrentVersion = currentVersion;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show($"Current Version: {CurrentVersion}", "Version Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
