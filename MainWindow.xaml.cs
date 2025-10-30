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
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewKeyUp += OnPreviewKeyUp;
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.VideoStream.StartAsync(port: 5600);
            }
        }
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 🔸 스페이스 키 차단 (UI 버튼에 전달되지 않도록)
            if (e.Key == Key.Space)
                e.Handled = true;

            (DataContext as ViewModels.MainViewModel)?.Control.OnKeyDown(e.Key);
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;

            (DataContext as ViewModels.MainViewModel)?.Control.OnKeyUp(e.Key);
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
                System.Diagnostics.Debug.WriteLine("[MainWindow] MainViewModel disposed manually.");
            }
        }


    }
}