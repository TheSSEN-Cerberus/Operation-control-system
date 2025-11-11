using GLib;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DateTime = System.DateTime;
using NetworkService = Operation_Control_System.Services.NetworkService;
using Task = System.Threading.Tasks.Task;
using Timer = System.Timers.Timer;

namespace Operation_Control_System.ViewModels
{
    public class VideoStreamViewModel : BaseViewModel
    {
        private readonly SharedStateService _shared;
        private readonly VideoStreamService _videoService;
        private readonly NetworkService _networkService;
        private readonly VideoRecorderService _recorder = new();
        private readonly ControlViewModel _controlViewModel;
        private readonly MapViewModel _mapViewModel;



        private readonly object _lock = new();
        private bool _started = false;

        private BitmapSource? _currentFrame;
        public BitmapSource? CurrentFrame
        {
            get => _currentFrame;
            set => SetProperty(ref _currentFrame, value);
        }

        // 현재 선택된 객체 정보 (기존 코드 유지 가능)
        private string _selectedBBoxInfo = "객체 정보 없음";
        public string SelectedBBoxInfo
        {
            get => _selectedBBoxInfo;
            set => SetProperty(ref _selectedBBoxInfo, value);
        }


        private DateTime _lastBBoxTime = DateTime.MinValue;
        private readonly Timer _bboxTimeoutTimer;

        private DateTime _lastFrameTime = DateTime.MinValue;
        private readonly Timer _frameTimeoutTimer;

        public ICommand TrackCommand { get; }
        public ICommand UntrackCommand { get; }

        public VideoStreamViewModel(NetworkService networkService, ControlViewModel controlViewModel, MapViewModel mapViewModel, SharedStateService shared)
        {
            _shared = shared;
            _mapViewModel = mapViewModel;
            _controlViewModel = controlViewModel;
            _networkService = networkService;
            _videoService = new VideoStreamService();
            _videoService.FrameArrived += OnFrameArrived;
            _networkService.BBoxReceived += OnBBoxReceived;
            _networkService.FireReadyReceived += OnFireReadyReceived;

            // ✅ 버튼 명령 초기화
            TrackCommand = new RelayCommand(OnTrack);
            UntrackCommand = new RelayCommand(OnUntrack);


            // ✅ 타임아웃 체크 타이머 (0.5초마다 검사)
            _bboxTimeoutTimer = new Timer(500);
            _bboxTimeoutTimer.Elapsed += (_, __) => CheckBBoxTimeout();
            _bboxTimeoutTimer.Start();

            // 프레임 체크 타이머
            _frameTimeoutTimer = new Timer(1000);
            _frameTimeoutTimer.Elapsed += (_, __) => CheckFrameTimeout();
            _frameTimeoutTimer.Start();
        }

        // fire_ready 수신 시 타깃 위치 계산
        private void OnFireReadyReceived(FireReadyData data)
        {
            if (data == null || data.TargetId == 0) return;
            if (!_mapViewModel.IsSet) return;

            var targetBox = _shared.BBoxes.FirstOrDefault(b => b.Id == data.TargetId);
            if (targetBox == null) return;

            double heading = _shared.Heading; // ControlViewModel에 저장된 heading 값
            double distance = data.Distance; // 단위: m
            var (lat, lon) = _mapViewModel.CalculateTargetPosition(_mapViewModel.Latitude, _mapViewModel.Longitude, heading, distance);

            targetBox.TargetLat = lat;
            targetBox.TargetLon = lon;

            Debug.WriteLine($"[FireReady] Target {data.TargetId} 위치: {lat:F6}, {lon:F6}");
        }

        // Gstreamer 영상 수신
        private async void OnFrameArrived(BitmapSource frame)
        {
            CurrentFrame = frame;
            _lastFrameTime = DateTime.UtcNow;
            if (!_recorder.IsRecording)
                await _recorder.StartAsync(frame);

            await _recorder.PushFrameAsync(frame, _shared.BBoxes);
        }

        // BBox 데이터 수신
        // ✅ BBox 데이터 수신
        private void OnBBoxReceived(BBoxData bboxData)
        {
            if (CurrentFrame == null || bboxData.Objects == null)
                return;


            _lastBBoxTime = DateTime.UtcNow;
            int frameWidth = CurrentFrame.PixelWidth;
            int frameHeight = CurrentFrame.PixelHeight;

            App.Current?.Dispatcher?.Invoke(() =>
            {
                // ① 현재 수신된 ID 목록
                var newIds = bboxData.Objects.Select(o => o.Id).ToHashSet();

                // ② 기존 중, 새 데이터에 없는 ID는 제거
                for (int i = _shared.BBoxes.Count - 1; i >= 0; i--)
                {
                    if (!newIds.Contains(_shared.BBoxes[i].Id))
                        _shared.BBoxes.RemoveAt(i);
                }

                // ③ 새로 들어온/기존 객체 갱신
                foreach (var model in bboxData.Objects)
                {
                    var existing = _shared.BBoxes.FirstOrDefault(b => b.Id == model.Id);
                    if (existing != null)
                    {
                        // 좌표, 색상 업데이트
                        existing.UpdatePos(model, frameWidth, frameHeight);
                        existing.UpdateColor();
                    }
                    else
                    {
                        var bboxVm = new BBoxViewModel(model, frameWidth, frameHeight);
                        bboxVm.Clicked += OnBBoxClicked;
                        // 새 객체 추가
                        _shared.BBoxes.Add(bboxVm);
                    }
                }

                // ④ 선택된 객체 정보 갱신 (예시)
                SelectedBBoxInfo = $"탐지 객체 수: {_shared.BBoxes.Count}";
            });
        }

        // ✅ 일정 시간 미수신 시 Clear
        private void CheckBBoxTimeout()
        {
            if ((DateTime.UtcNow - _lastBBoxTime).TotalMilliseconds > 300)
            {
                App.Current?.Dispatcher?.Invoke(() => _shared.BBoxes.Clear());
            }
        }

        private void CheckFrameTimeout()
        {
            if (CurrentFrame == null)
                return;

            if ((DateTime.UtcNow - _lastFrameTime).TotalSeconds > 1)
            {
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    CurrentFrame = null;
                });
            }
        }

        public void OnBBoxClicked(int id)
        {
            // 수동 모드일 때만 추적 명령 전송
            if (_controlViewModel.SelectedOperationMode == "수동")
            {
                _controlViewModel.TrackedTargetId = id;

                var target = _shared.BBoxes.FirstOrDefault(x => x.Id == id);
                if (target != null)
                {
                    // ✅ 클릭된 BBox의 추적 상태 업데이트
                    target.IsTracked = true;

                    Debug.WriteLine($"[VideoStream] 🖱️ BBox clicked → ID={id}, IsTracked=True");
                }

                _ = _networkService.SendAsync(new Message<TrackTargetData>
                {
                    Type = "track_target",
                    Data = new TrackTargetData { TargetId = id }
                });
            }
        }

        // 🔸 추적 명령
        private async void OnTrack(object? param)
        {
            if (_controlViewModel.SelectedOperationMode == "수동")
            {
                if (param is int id)
                {
                    var target = _shared.BBoxes.FirstOrDefault(x => x.Id == id);
                    if (target != null)
                    {
                        target.IsTracked = true;

                        await _networkService.SendAsync(new Message<TrackTargetData>
                        {
                            Type = "track_target",
                            Data = new TrackTargetData { TargetId = id }
                        });

                        Debug.WriteLine($"[VideoStream] 🎯 Track start → ID={id}");
                    }
                }
            }
        }

        // 🔸 추적 해제
        private async void OnUntrack(object? param)
        {
            if (param is int id)
            {
                var target = _shared.BBoxes.FirstOrDefault(x => x.Id == id);
                if (target != null)
                {
                    target.IsTracked = false;

                    await _networkService.SendAsync(new Message<TrackReleaseData>
                    {
                        Type = "track_release",
                        Data = new TrackReleaseData { TargetId= id }
                    });

                    System.Diagnostics.Debug.WriteLine($"[VideoStream] 🟢 Track released → ID={id}");
                }
            }
        }

         //🔸 탐지 제거

        public async Task StartAsync(int port = 5600)
        {
            lock (_lock)
            {
                if (_started)
                {
                    System.Diagnostics.Debug.WriteLine("[VideoStream] ⚠️ Already started. Skipping duplicate call.");
                    return;
                }
                _started = true;
            }

            await Task.Run(() =>
            {
                try
                {
                    _videoService.Start(port);
                    System.Diagnostics.Debug.WriteLine($"[VideoStream] Listening on UDP {port}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoStream] Error starting: {ex.Message}");
                }
            });
        }

        public void Stop()
        {
            _videoService.Stop();
            _recorder.Stop();
            _bboxTimeoutTimer.Close();
            _frameTimeoutTimer.Close();
        }
        public void Dispose()
        {
            Stop();
        }

    }
}