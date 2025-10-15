using System.Collections.ObjectModel;
using System.Windows.Media;
using Operation_Control_System.Infrastructure;

namespace Operation_Control_System.ViewModels
{
    public class ControlViewModel : BaseViewModel
    {
        // --- 상태 속성 ---
        private bool _isLaserOn;
        public bool IsLaserOn
        {
            get => _isLaserOn;
            set
            {
                if (SetProperty(ref _isLaserOn, value))
                    OnPropertyChanged(nameof(LaserToggleText));
            }
        }
        public string LaserToggleText => IsLaserOn ? "ON" : "OFF";

        private bool _isAutoFire;
        public bool IsAutoFire
        {
            get => _isAutoFire;
            set
            {
                if (SetProperty(ref _isAutoFire, value))
                    OnPropertyChanged(nameof(FireModeText));
            }
        }
        public string FireModeText => IsAutoFire ? "자동" : "수동";

        public ObservableCollection<string> OperationModes { get; } =
            new() { "수동", "반자동", "자동" };

        private string _selectedOperationMode = "수동";
        public string SelectedOperationMode
        {
            get => _selectedOperationMode;
            set => SetProperty(ref _selectedOperationMode, value);
        }

        // --- 움직임 표시 색상 ---
        public Brush MoveForwardColor => Brushes.Gray;
        public Brush MoveBackwardColor => Brushes.Gray;
        public Brush MoveLeftColor => Brushes.Gray;
        public Brush MoveRightColor => Brushes.Gray;

        public Brush GimbalUpColor => Brushes.Gray;
        public Brush GimbalDownColor => Brushes.Gray;
        public Brush GimbalLeftColor => Brushes.Gray;
        public Brush GimbalRightColor => Brushes.Gray;
    }
}
