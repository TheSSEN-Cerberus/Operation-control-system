using Operation_Control_System.Infrastructure;

namespace Operation_Control_System.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public NetworkViewModel Network { get; set; } = new();
        public ControlViewModel Control { get; set; } = new();
        public MapViewModel Map { get; set; } = new();
        public VideoStreamViewModel VideoStream { get; set; } = new();
        public MainViewModel()
        {
            // ✅ 프로그램 시작 시 자동으로 영상 수신 시작
            //_ = VideoStream.StartAsync(port: 5600);
        }
    }
}