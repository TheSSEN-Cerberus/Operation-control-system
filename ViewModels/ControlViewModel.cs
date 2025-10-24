using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Services;

namespace Operation_Control_System.ViewModels
{
    public class ControlViewModel : BaseViewModel
    {
        private readonly NetworkService _networkService;

        // --- 기존 상태 속성 (레이저, 모드 등)
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

        // --- [신규] 표적/격발 관련 ---
        private int? _trackedTargetId;
        public int? TrackedTargetId
        {
            get => _trackedTargetId;
            set => SetProperty(ref _trackedTargetId, value);
        }

        private bool _canFire;
        public bool CanFire
        {
            get => _canFire;
            set
            {
                if (SetProperty(ref _canFire, value))
                    OnPropertyChanged(nameof(FireStatusText));
            }
        }

        // 격발 상태 텍스트 (버튼 옆에 표시)
        public string FireStatusText => CanFire ? "(가능)" : "(불가)";

        private bool? _lastFireHit;
        public bool? LastFireHit
        {
            get => _lastFireHit;
            set => SetProperty(ref _lastFireHit, value);
        }

        public ICommand ConfirmKillCommand { get; }
        public ICommand SendFireCommand { get; }

        public ControlViewModel(NetworkService networkService)
        {
            _networkService = networkService;
            ConfirmKillCommand = new RelayCommand(OnConfirmKill);
            SendFireCommand = new RelayCommand(OnSendFire);
            this._networkService = networkService;
        }

        private void OnConfirmKill(object? param)
        {
            if (param is string result)
            {
                bool isHit = result.Equals("true", StringComparison.OrdinalIgnoreCase);
                LastFireHit = isHit;
                Console.WriteLine($"[Control] Fire result: {(isHit ? "HIT" : "MISS")}");
            }
        }

        private void OnSendFire(object? param)
        {
            if (!CanFire)
            {
                Console.WriteLine("[Control] Fire attempt ignored (not ready).");
                return;
            }

            Console.WriteLine("[Control] Fire command sent!");
            CanFire = false; // 예시로 바로 비활성화 (재준비 대기)
        }
    }
}
