using Operation_Control_System.ViewModels;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Operation_Control_System
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var vm = new MainViewModel();
            var mainWindow = new MainWindow { DataContext = vm };
            mainWindow.Show();
        }
    }
}
