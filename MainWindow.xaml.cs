using Operation_Control_System.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Operation_Control_System
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void OnBBoxClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is BBoxViewModel bbox)
            {
                if (DataContext is MainViewModel vm)
                    vm.VideoStream.OnBBoxClicked(bbox.Id);
            }
        }
    }
}