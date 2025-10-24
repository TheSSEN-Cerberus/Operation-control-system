using Operation_Control_System.Infrastructure;
using Operation_Control_System.Services;

namespace Operation_Control_System.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public NetworkViewModel Network { get; }
        public ControlViewModel Control { get; }
        public MapViewModel Map { get; }
        public VideoStreamViewModel VideoStream { get; }

        private readonly NetworkService _networkService;
        public MainViewModel()
        {
            // 단일 네트워크 서비스
            _networkService = new NetworkService();

            Network = new NetworkViewModel(_networkService);
            VideoStream = new VideoStreamViewModel(_networkService);
            Control = new ControlViewModel(_networkService);
            Map = new MapViewModel(_networkService);
            // ✅ 프로그램 시작 시 자동으로 영상 수신 시작
            _ = VideoStream.StartAsync(port: 5600);
        }
    }
}