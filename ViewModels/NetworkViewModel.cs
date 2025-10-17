using Operation_Control_System.Services;
using Operation_Control_System.ViewModels;
using System.Windows;
using System.Windows.Media;

public class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _service;

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public string StatusText => IsConnected ? "(연결됨)" : "(연결 끊김)";
    public Brush StatusColor => IsConnected ? Brushes.LimeGreen : Brushes.Red;

    public NetworkViewModel()
    {
        _service = new NetworkService();

        _service.ConnectionChanged += (connected) =>
        {
            Application.Current.Dispatcher.Invoke(() => IsConnected = connected);
        };

        _ = _service.StartAsync();
    }
}
