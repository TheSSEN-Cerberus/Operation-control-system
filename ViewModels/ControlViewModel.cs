using Gst;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;
using Task = System.Threading.Tasks.Task;

namespace Operation_Control_System.ViewModels
{
    public class ControlViewModel : BaseViewModel
    {
        private readonly NetworkService _networkService;

        // --- 레이저 제어 ---
        private bool _isLaserOn;
        public bool IsLaserOn
        {
            get => _isLaserOn;
            set
            {
                if (SetProperty(ref _isLaserOn, value))
                {
                    OnPropertyChanged(nameof(LaserToggleText));
                    _ = SendLaserCommand(value);
                }
            }
        }
        public string LaserToggleText => IsLaserOn ? "ON" : "OFF";

        // --- 격발 모드 ---
        private bool _isAutoFire;
        public bool IsAutoFire
        {
            get => _isAutoFire;
            set
            {
                if (SetProperty(ref _isAutoFire, value))
                {
                    OnPropertyChanged(nameof(FireModeText));
                    _ = SendFireModeChange(value);
                }
            }
        }
        public string FireModeText => IsAutoFire ? "자동" : "수동";

        // --- 운용모드 ---
        public ObservableCollection<string> OperationModes { get; } =
            new() { "수동", "반자동", "자동" };

        private string _selectedOperationMode = "수동";
        public string SelectedOperationMode
        {
            get => _selectedOperationMode;
            set
            {
                if (SetProperty(ref _selectedOperationMode, value))
                {
                    _ = SendOperationModeChange(value);
                }
            }
        }

        // --- 움직임 상태 색상 (추후 연동 가능) ---
        public Brush MoveForwardColor => Brushes.Gray;
        public Brush MoveBackwardColor => Brushes.Gray;
        public Brush MoveLeftColor => Brushes.Gray;
        public Brush MoveRightColor => Brushes.Gray;
        public Brush GimbalUpColor => Brushes.Gray;
        public Brush GimbalDownColor => Brushes.Gray;
        public Brush GimbalLeftColor => Brushes.Gray;
        public Brush GimbalRightColor => Brushes.Gray;

        // --- 표적 및 격발 관련 ---
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
        public string FireStatusText => CanFire ? "(가능)" : "(불가)";

        private bool? _lastFireHit;
        public bool? LastFireHit
        {
            get => _lastFireHit;
            set => SetProperty(ref _lastFireHit, value);
        }

        // --- 명령 ---
        public ICommand ConfirmKillCommand { get; }
        public ICommand SendFireCommand { get; }

        // --- 생성자 ---
        public ControlViewModel(NetworkService networkService)
        {
            _networkService = networkService;

            ConfirmKillCommand = new RelayCommand(OnConfirmKill);
            SendFireCommand = new RelayCommand(OnSendFire);

            // ✅ 보드에서 격발 준비 신호만 수신 (FireReady)
            _networkService.FireReadyReceived += OnFireReadyReceived;
            _networkService.TrackTargetReceived += OnTrackTargetReceived;

        }



        // --- 보드에서 격발 준비 완료 수신 시 ---
        private void OnFireReadyReceived(FireReadyData data)
        {
            CanFire = data.IsReady;
            Debug.WriteLine($"[Control] Fire Ready = {CanFire}");
        }

        // --- 운용자 GUI에서 타격 성공/실패 판단 시 (보드로 전송) ---
        private async void OnConfirmKill(object? param)
        {
            if (param is string result)
            {
                bool isHit = result.Equals("true", StringComparison.OrdinalIgnoreCase);
                LastFireHit = isHit;
                Debug.WriteLine($"[Control] GUI confirmed: {(isHit ? "HIT ✅" : "MISS ❌")}");
                try
                {
                    await _networkService.SendAsync(new Message<FireResultData>
                    {
                        Type = "fire_result",
                        Data = new FireResultData
                        {
                            Success = isHit
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Control] FireResult send error: {ex.Message}");
                }
            }
        }

        // --- 격발 명령 송신 ---
        private async void OnSendFire(object? param)
        {
            if (!CanFire)
            {
                Debug.WriteLine("[Control] ⚠️ Fire attempt ignored (not ready).");
                return;
            }
            try
            {
                await _networkService.SendAsync(new Message<FireCommandData>
                {
                    Type = "fire_command",
                    Data = new FireCommandData
                    {
                        Trigger = true
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] Fire send error: {ex.Message}");
            }
        }

        private void OnTrackTargetReceived(TrackTargetData data)
        {
            if(_selectedOperationMode == "수동")
            {
                return;
            }
            TrackedTargetId = data.TargetId;
            Console.WriteLine($"[Control] 🎯 Tracked Target ID updated from board: {data.TargetId}");
        }
        private async Task SendLaserCommand(bool isOn)
        {
            try
            {
                await _networkService.SendAsync(new Message<LaserControlData>
                {
                    Type = "laser_control",
                    Data = new LaserControlData { isOn = isOn }
                });
                Debug.WriteLine($"[Control] Laser {(isOn ? "ON" : "OFF")} sent.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] Laser send error: {ex.Message}");
            }
        }

        private async Task SendFireModeChange(bool isAuto)
        {
            try
            {
                await _networkService.SendAsync(new Message<FireSelectorModeData>
                {
                    Type = "fire_mode",
                    Data = new FireSelectorModeData { Mode = isAuto ? 1 : 0 }
                });
                Debug.WriteLine($"[Control] Fire mode {(isAuto ? "AUTO" : "MANUAL")} sent.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] FireMode send error: {ex.Message}");
            }
        }

        private async Task SendOperationModeChange(string mode)
        {
            try
            {
                int int_mode;
                switch (mode)
                {
                    case "수동":
                        int_mode = 0;
                        break;
                    case "반자동":
                        int_mode = 1;
                        break;
                    case "자동":
                        int_mode = 2;
                        break;
                    default:
                        Debug.WriteLine($"[Control] Operation mode {mode}");
                        return;
                }
                await _networkService.SendAsync(new Message<OperationModeData>
                {
                    Type = "operation_mode",
                    Data = new OperationModeData { Mode = int_mode }
                });
                Debug.WriteLine($"[Control] Operation mode {int_mode} sent.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] OperationMode send error: {ex.Message}");
            }
        }
    }
}
