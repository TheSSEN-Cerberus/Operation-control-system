using Operation_Control_System.Infrastructure;

namespace Operation_Control_System.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public NetworkViewModel Network { get; set; } = new();
        public ControlViewModel Control { get; set; } = new();
        public MapViewModel Map { get; set; } = new();
        public VideoStreamViewModel VideoStream { get; set; } = new();

    }
}