using Gst;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Debug = System.Diagnostics.Debug;
using Task = System.Threading.Tasks.Task;
using System.Collections.Generic;
using DateTime = System.DateTime;
using System.Reflection.Metadata.Ecma335;


namespace Operation_Control_System.ViewModels
{
    public class ControlViewModel : BaseViewModel
    {

        private readonly DispatcherTimer _moveTimer;
        private DateTime _lastUpdateTime;
        private const double RobotSpeedCmPerSec = 54.3; // 이동속도 [cm/s]


        // --- 의존성 주입 필드 ---
        private readonly NetworkService _networkService;
        private readonly BluetoothService _bluetoothService;
        private readonly MapViewModel _mapViewModel;
        // --- 생성자 ---
        public ControlViewModel(NetworkService networkService, BluetoothService bluetoothService, MapViewModel mapViewModel)
        {
            _networkService = networkService;
            _bluetoothService = bluetoothService;
            _mapViewModel = mapViewModel;

            _moveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _moveTimer.Tick += OnMoveTick;


            // ✅ 명령 초기화
            SendFireCommand = new RelayCommand(OnSendFire);

            // ✅ 네트워크 서비스 이벤트 구독
            _networkService.FireReadyReceived += OnFireReadyReceived;
            _networkService.TrackTargetReceived += OnTrackTargetReceived;

            _ = _bluetoothService.StartAutoConnectAsync();


        }

        // ====================================================================================================
        // --- 속성 (Properties) ---
        // ====================================================================================================


        // --- 레이저 제어 ---
        private bool _isLaserOn = true;
        /// <summary>
        /// 레이저 ON/OFF 상태를 나타냅니다.
        /// </summary>
        public bool IsLaserOn
        {
            get => _isLaserOn;
            set
            {
                if (SetProperty(ref _isLaserOn, value))
                {
                    OnPropertyChanged(nameof(LaserToggleText));
                    _ = SendLaserCommand(value); // 레이저 제어 명령 전송
                }
            }
        }
        /// <summary>
        /// 레이저 ON/OFF 상태에 따른 텍스트를 반환합니다.
        /// </summary>
        public string LaserToggleText => IsLaserOn ? "ON" : "OFF";



        // --- 운용 모드 ---
        /// <summary>
        /// 선택 가능한 운용 모드 목록입니다.
        /// </summary>
        private bool _isAutoOperation;
        public bool IsAutoOperation
        {
            get => _isAutoOperation;
            set
            {
                if (SetProperty(ref _isAutoOperation, value))
                {
                    OnPropertyChanged(nameof(OperationModeText));
                    _ = SendOperationModeChange(value); // 레이저 제어 명령 전송
                }
            }
        }

        public string OperationModeText => IsAutoOperation ? "자동" : "수동";

        /// <summary>
        /// 현재 선택된 운용 모드를 나타냅니다.
        /// </summary>
       

        // --- 움직임 상태 색상 (UI 피드백) ---
        private Brush _moveForwardColor = Brushes.Gray;
        /// <summary>
        /// 전진 버튼의 색상을 나타냅니다.
        /// </summary>
        public Brush MoveForwardColor
        {
            get => _moveForwardColor;
            set => SetProperty(ref _moveForwardColor, value);
        }

        // --- 김발 방향 색상 (UI 피드백) ---
        private Brush _gimbalUpColor = Brushes.Gray;
        /// <summary>
        /// 김발 위쪽 방향 버튼의 색상을 나타냅니다.
        /// </summary>
        public Brush GimbalUpColor
        {
            get => _gimbalUpColor;
            set => SetProperty(ref _gimbalUpColor, value);
        }

        private Brush _gimbalDownColor = Brushes.Gray;
        /// <summary>
        /// 김발 아래쪽 방향 버튼의 색상을 나타냅니다.
        /// </summary>
        public Brush GimbalDownColor
        {
            get => _gimbalDownColor;
            set => SetProperty(ref _gimbalDownColor, value);
        }

        private Brush _gimbalLeftColor = Brushes.Gray;
        /// <summary>
        /// 김발 왼쪽 방향 버튼의 색상을 나타냅니다.
        /// </summary>
        public Brush GimbalLeftColor
        {
            get => _gimbalLeftColor;
            set => SetProperty(ref _gimbalLeftColor, value);
        }

        private Brush _gimbalRightColor = Brushes.Gray;
        /// <summary>
        /// 김발 오른쪽 방향 버튼의 색상을 나타냅니다.
        /// </summary>
        public Brush GimbalRightColor
        {
            get => _gimbalRightColor;
            set => SetProperty(ref _gimbalRightColor, value);
        }

        // --- 표적 및 격발 관련 ---
        private int? _trackedTargetId;
        /// <summary>
        /// 현재 추적 중인 표적의 ID를 나타냅니다. (null이면 추적 중인 표적 없음)
        /// </summary>
        public int? TrackedTargetId
        {
            get => _trackedTargetId;
            set => SetProperty(ref _trackedTargetId, value);
        }

        private bool _fireReady = false; // 보드에서 수신하는 격발 준비 상태
        private bool _canFire;
        /// <summary>
        /// 현재 격발이 가능한지 여부를 나타냅니다. (UI 활성화/비활성화에 사용)
        /// </summary>
        public bool CanFire
        {
            get => _canFire;
            set => SetProperty(ref _canFire, value);
        }

        // --- 로봇 이동 상태 ---
        private int _movingState = 0;  // 0: 정지, 1: 전진 (기본값 0)
        /// <summary>
        /// 로봇의 이동 상태를 나타냅니다. (0: 정지, 1: 전진)
        /// </summary>
        public int MovingState
        {
            get => _movingState;
            set => SetProperty(ref _movingState, value);
        }

        // ====================================================================================================
        // --- 명령 (Commands) ---
        // ====================================================================================================

        /// <summary>
        /// 격발 명령을 보드에 전송하는 명령입니다.
        /// </summary>
        public ICommand SendFireCommand { get; }

        // ====================================================================================================
        // --- 비공개 필드 (Private Fields) ---
        // ====================================================================================================

        // --- 김발 제어 관련 ---
        private readonly Dictionary<Key, DispatcherTimer> _keyTimers = new(); // 키 반복 입력을 위한 타이머 딕셔너리
        // private Key? _pressedKey; // 현재 사용되지 않음.

        // ====================================================================================================
        // --- 공개 메서드 (Public Methods) ---
        // ====================================================================================================

        /// <summary>
        /// 보드로부터 격발 준비 신호를 수신했을 때 호출됩니다.
        /// </summary>
        /// <param name="ready">격발 준비 상태 (true: 준비 완료, false: 준비 안됨)</param>
        public void SetFireReady(bool ready)
        {
            App.Current?.Dispatcher?.Invoke(() =>
            {
                _fireReady = ready;
                UpdateCanFire(); // 격발 가능 여부 업데이트
            });

        }

        /// <summary>
        /// 키보드 눌림 이벤트를 처리합니다.
        /// </summary>
        /// <param name="key">눌린 키</param>
        public void OnKeyDown(Key key)
        {

            // ===============================
            // 🔸 운용모드에 따른 키 입력 제한
            // ===============================
            // 🚫 자동 모드일 때는 김발 조작(WASD) 무시
            if (IsAutoOperation == true &&
                (key == Key.W || key == Key.A || key == Key.S || key == Key.D))
            {
                Debug.WriteLine("[Control] Gimbal key ignored (비수동 모드).");
                return;
            }

            // 🔸 R키: 로봇 이동 토글
            if (key == Key.R)
            {
                ToggleMoving();
                return;
            }
            // 🔸 스페이스바: 격발 명령 시도
            if (key == Key.Space)
            {
                TryFireCommand();
                return;
            }

            // 김발 제어 키 (W, S, A, D)
            if (_keyTimers.ContainsKey(key))
                return; // 이미 해당 키의 타이머가 실행 중이라면 무시

            SetDirectionColor(key, true); // 김발 방향 색상 ON

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80) // 80ms 간격으로 반복 전송
            };
            timer.Tick += (_, __) => HandleGimbalKey(key);
            _keyTimers[key] = timer;
            timer.Start();

            HandleGimbalKey(key); // 즉시 1회 송신
        }

        /// <summary>
        /// 키보드 떼기 이벤트를 처리합니다.
        /// </summary>
        /// <param name="key">떼어진 키</param>
        public void OnKeyUp(Key key)
        {
            if (_keyTimers.TryGetValue(key, out var timer))
            {
                timer.Stop();
                _keyTimers.Remove(key);
            }
            SetDirectionColor(key, false); // 김발 방향 색상 OFF
        }

        // ====================================================================================================
        // --- 비공개 메서드 (Private Methods) ---
        // ====================================================================================================

        /// <summary>
        /// 현재 격발 준비 상태와 운용 모드를 기반으로 격발 가능 여부를 업데이트합니다.
        /// </summary>
        private void UpdateCanFire()
        {
            CanFire = _fireReady;
        }

        /// <summary>
        /// 김발 방향 키에 따른 UI 색상을 업데이트합니다.
        /// </summary>
        /// <param name="key">방향 키</param>
        /// <param name="isActive">활성화 여부</param>
        private void SetDirectionColor(Key key, bool isActive)
        {
            Brush color = isActive ? Brushes.LimeGreen : Brushes.Gray;

            switch (key)
            {
                case Key.W: GimbalUpColor = color; break;
                case Key.S: GimbalDownColor = color; break;
                case Key.A: GimbalLeftColor = color; break;
                case Key.D: GimbalRightColor = color; break;
            }
        }

        /// <summary>
        /// 격발 명령을 시도합니다. 격발 준비가 되어 있지 않으면 무시됩니다.
        /// </summary>
        private void TryFireCommand()
        {
            //if (!CanFire)
            //{
            //    Console.WriteLine("[Control] Fire command ignored (not ready).");
            //    return;
            //}
            OnSendFire(null);
        }

        /// <summary>
        /// 로봇 이동 상태를 토글하고 명령을 전송합니다.
        /// </summary>
        public void ToggleMoving()
        {
            if (MovingState == 0)
            {
                // 🔹 전진 시작
                MovingState = 1;
                _lastUpdateTime = DateTime.Now;
                _moveTimer.Start();

                MoveForwardColor = Brushes.LimeGreen;
                
                // BLE 명령
                if (_bluetoothService.IsConnected)
                    _ = _bluetoothService.SendCommandAsync(_bluetoothService.MoveCommand);
            }
            else
            {
                // 🔹 정지
                MovingState = 0;
                _moveTimer.Stop();

                MoveForwardColor = Brushes.Gray;

                if (_bluetoothService.IsConnected)
                    _ = _bluetoothService.SendCommandAsync(_bluetoothService.StopCommand);
            }

            Debug.WriteLine($"[Control] Moving → {MovingState}");
        }



        private void OnMoveTick(object? sender, EventArgs e)
        {
            if (MovingState == 1 && _bluetoothService.IsConnected)
            {
                var now = DateTime.Now;
                double elapsed = (now - _lastUpdateTime).TotalSeconds;
                _lastUpdateTime = now;

                double distance = RobotSpeedCmPerSec * elapsed;
                _mapViewModel.UpdateRobotPosition(distance);
            }
        }


        /// <summary>
        /// 김발 제어 방향 키 입력을 처리하고 증분 명령을 전송합니다.
        /// </summary>
        /// <param name="key">눌린 방향 키</param>
        private void HandleGimbalKey(Key key)
        {
            float dY = 0, dP = 0;

            switch (key)
            {
                case Key.W: dP = -2; break; // 위로
                case Key.S: dP = +2; break; // 아래로
                case Key.A: dY = -2; break; // 왼쪽으로
                case Key.D: dY = +2; break; // 오른쪽으로
                default: return;
            }

            _ = SendGimbalCommandAsync(dY, dP); // 김발 증분 명령 전송
        }

        /// <summary>
        /// 김발 증분 명령을 비동기적으로 전송합니다.
        /// </summary>
        /// <param name="dAz">방위각 증분 값</param>
        /// <param name="dEl">고각 증분 값</param>
        /// <returns>비동기 작업</returns>
        private async Task SendGimbalCommandAsync(float dY, float dP)
        {
            try
            {
                await _networkService.SendAsync(new Message<GimbalControlData>
                {
                    Type = "gimbal_control",
                    Data = new GimbalControlData { Yaw = dY, Pitch = dP }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Control] Gimbal send error: {ex.Message}");
            }
        }

        /// <summary>
        /// 보드로부터 격발 준비 완료 신호를 수신했을 때 실행됩니다.
        /// </summary>
        /// <param name="data">격발 준비 데이터</param>
        private void OnFireReadyReceived(FireReadyData data)
        {
            SetFireReady(true); // 격발 준비 상태 업데이트
            Debug.WriteLine($"[Control] Fire Ready = {CanFire}");
        }




        /// <summary>
        /// 격발 명령을 보드에 전송합니다.
        /// </summary>
        /// <param name="param">현재 사용되지 않음</param>
        private async void OnSendFire(object? param)
        {
            //if (!CanFire)
            //{
            //    Debug.WriteLine("[Control] ⚠️ Fire attempt ignored (not ready).");
            //    return;
            //}
            try
            {
                await _networkService.SendAsync(new Message<FireCommandData>
                {
                    Type = "fire_command",
                    Data = new FireCommandData
                    {
                        Trigger = true // 격발 트리거
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] Fire send error: {ex.Message}");
            }
            finally
            {
                _fireReady = false;
                // 🔸 1초 후 다시 상태 갱신
                UpdateCanFire();
            }
        }

        /// <summary>
        /// 보드로부터 추적 타겟 정보를 수신했을 때 실행됩니다. (수동 모드 제외)
        /// </summary>
        /// <param name="data">추적 타겟 데이터</param>
        private void OnTrackTargetReceived(TrackTargetData data)
        {
            if (IsAutoOperation == false) // 수동 모드에서는 타겟 추적 정보를 무시
            {
                return;
            }
            TrackedTargetId = data.TargetId; // 추적 타겟 ID 업데이트
            Debug.WriteLine($"[Control] 🎯 Tracked Target ID updated from board: {data.TargetId}");
        }

        /// <summary>
        /// 레이저 ON/OFF 명령을 비동기적으로 전송합니다.
        /// </summary>
        /// <param name="isOn">레이저 ON 여부</param>
        /// <returns>비동기 작업</returns>
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



        /// <summary>
        /// 운용 모드 변경 명령을 비동기적으로 전송합니다.
        /// </summary>
        /// <param name="mode">변경할 운용 모드 ("수동", "반자동", "자동")</param>
        /// <returns>비동기 작업</returns>
        private async Task SendOperationModeChange(bool isAuto)
        {
            try
            {
                await _networkService.SendAsync(new Message<OperationModeData>
                {
                    Type = "operation_mode",
                    Data = new OperationModeData { Mode = isAuto ? 1 : 0 } // 1: 자동, 0: 수동
                });
                Debug.WriteLine($"[Control] Operation mode {(isAuto ? "AUTO" : "MANUAL")} sent.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Control] FireMode send error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _bluetoothService?.Dispose();
        }
    }
}