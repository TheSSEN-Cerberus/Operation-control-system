using Operation_Control_System.Services;
using Operation_Control_System.ViewModels;
using System.Windows;
using System.Windows.Media;

public class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _networkService;
    private readonly BluetoothService _bluetoothService;


    private bool _networkIsConnected;
    public bool NetworkIsConnected
    {
        get => _networkIsConnected;
        set
        {
            if (SetProperty(ref _networkIsConnected, value))
            {
                OnPropertyChanged(nameof(NetworkStatusColor));
            }
        }
    }

    public Brush NetworkStatusColor => _networkIsConnected ? Brushes.LimeGreen : Brushes.Red;

    private bool _bleIsConnected;
    public bool BleIsConnected
    {
        get => _bleIsConnected;
        set
        {
            if (SetProperty(ref _bleIsConnected, value))
            {
                OnPropertyChanged(nameof(BleStatusColor));
            }
        }
    }

    public Brush BleStatusColor => _bleIsConnected ? Brushes.LimeGreen : Brushes.Red;

    public NetworkViewModel(NetworkService networkService, BluetoothService bluetoothService)
    {
        _networkService = networkService;
        _bluetoothService = bluetoothService;


        _networkService.ConnectionChanged += (connected) =>
        {
            Application.Current?.Dispatcher?.Invoke(() => NetworkIsConnected = connected);
        };

        _bluetoothService.ConnectionChanged += (connected) =>
        {
            Application.Current?.Dispatcher?.Invoke(() => BleIsConnected = connected);
        };

        _ = _networkService.StartAsync();
    }
    public void Dispose()
    {
        _networkService?.Dispose();
    }
}
