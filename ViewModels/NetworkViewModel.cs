using Operation_Control_System.Services;
using Operation_Control_System.ViewModels;
using System.Windows;
using System.Windows.Media;

public class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _networkService;

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

    public NetworkViewModel(NetworkService networkService)
    {
        _networkService = networkService;

        _networkService.ConnectionChanged += (connected) =>
        {
            Application.Current?.Dispatcher?.Invoke(() => IsConnected = connected);
        };

        _ = _networkService.StartAsync();
    }
    public void Dispose()
    {
        _networkService?.Dispose();
    }
}
