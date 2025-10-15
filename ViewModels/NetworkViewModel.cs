using System.Windows.Media;

namespace Operation_Control_System.ViewModels
{
    public class NetworkViewModel : BaseViewModel
    {
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public Brush StatusColor => IsConnected ? Brushes.Green : Brushes.Red;
        public string StatusText => IsConnected ? "(연결됨)" : "(연결 끊김)";
    }
}
