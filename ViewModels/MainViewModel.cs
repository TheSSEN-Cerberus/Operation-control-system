using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System.Diagnostics;

namespace Operation_Control_System.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        public SharedStateService Shared { get; }
        public NetworkViewModel Network { get; }
        public ControlViewModel Control { get; }
        public MapViewModel Map { get; }
        public VideoStreamViewModel VideoStream { get; }

        private readonly NetworkService _networkService;
        private readonly BluetoothService _bluetoothService;

        public RelayCommand EmergencyStopCommand { get; }
        public MainViewModel()
        {
            Debug.WriteLine($"[VM] MainViewModel created at {DateTime.Now:HH:mm:ss.fff}, Thread={Environment.CurrentManagedThreadId}");

            Shared = new SharedStateService();

            // 단일 네트워크 서비스
            _networkService = new NetworkService();
            _bluetoothService = new BluetoothService();

            Map = new MapViewModel(_networkService, Shared);
            Network = new NetworkViewModel(_networkService, _bluetoothService);
            Control = new ControlViewModel(_networkService, _bluetoothService, Map);
            VideoStream = new VideoStreamViewModel(_networkService, Control, Map, Shared);
            EmergencyStopCommand = new RelayCommand(async _ => await ExecuteEmergencyStop());
        }

        /// <summary>
        /// 🚨 비상정지 실행
        /// </summary>
        public async Task ExecuteEmergencyStop()
        {
            try
            {
                Debug.WriteLine("[System] 🚨 EMERGENCY STOP TRIGGERED.");

                // 1️⃣ 보드로 비상정지 명령 송신
                await _networkService.SendAsync(new Message<EmergencyStopData>
                {
                    Type = "soft_reset",
                    Data = new EmergencyStopData { reset = true }
                });

                // 2️⃣ GUI 상태 초기화 (네트워크 제외)
                ResetAllExceptNetwork();

                // 3️⃣ 사용자에게 피드백
                Debug.WriteLine("[System] All states reset except Network.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[System] Emergency stop error: {ex.Message}");
            }
        }

        /// <summary>
        /// 네트워크 제외 모든 상태 초기화
        /// </summary>
        private void ResetAllExceptNetwork()
        {
            // Control 상태 초기화
            Control.IsLaserOn = false;
            Control.IsAutoFire = false;
            Control.IsAutoOperation = false;
            Control.TrackedTargetId = null;
            Control.CanFire = false;

            // 임무장비 초기화
            if (Control.MovingState == 1)
            {
                Control.ToggleMoving();
            }

            // 색상 초기화
            Control.MoveForwardColor = System.Windows.Media.Brushes.Gray;
            Control.GimbalUpColor = System.Windows.Media.Brushes.Gray;
            Control.GimbalDownColor = System.Windows.Media.Brushes.Gray;
            Control.GimbalLeftColor = System.Windows.Media.Brushes.Gray;
            Control.GimbalRightColor = System.Windows.Media.Brushes.Gray;

            // Video 초기화
            
            VideoStream.SelectedBBoxInfo = "객체 정보 없음";
            Debug.WriteLine("[System] ✅ State Reset Done (Network kept alive)");


        }

        public void Dispose()
        {

            VideoStream?.Dispose();
            Network?.Dispose();
            Control?.Dispose();
            Shared.ResetAll();
            //Map.Dispose();
        }
    }
}