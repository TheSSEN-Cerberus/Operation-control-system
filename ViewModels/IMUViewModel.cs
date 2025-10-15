using Operation_Control_System.ViewModels;

namespace Operation_Control_System.ViewModels
{
    public class IMUViewModel : BaseViewModel
    {
        private double _roll;
        public double Roll
        {
            get => _roll;
            set => SetProperty(ref _roll, value);
        }

        private double _pitch;
        public double Pitch
        {
            get => _pitch;
            set => SetProperty(ref _pitch, value);
        }

        private double _yaw;
        public double Yaw
        {
            get => _yaw;
            set => SetProperty(ref _yaw, value);
        }

        private double _ax, _ay, _az;
        public string AccelText => $"X:{_ax:F1} Y:{_ay:F1} Z:{_az:F1}";

        public void UpdateAccel(double ax, double ay, double az)
        {
            _ax = ax; _ay = ay; _az = az;
            OnPropertyChanged(nameof(AccelText));
        }
    }
}
