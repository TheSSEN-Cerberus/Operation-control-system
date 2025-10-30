using GLib;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Timers;

using NetworkService = Operation_Control_System.Services.NetworkService;
using Task = System.Threading.Tasks.Task;
using DateTime = System.DateTime;
using Timer = System.Timers.Timer;

namespace Operation_Control_System.ViewModels
{
    public class VideoStreamViewModel : BaseViewModel
    {
        private readonly VideoStreamService _videoService;
        private readonly NetworkService _networkService;
        private readonly VideoRecorderService _recorder = new();

        private readonly ControlViewModel _controlViewModel;


        public ObservableCollection<BBoxViewModel> BBoxes { get; } = new();

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

        public VideoStreamViewModel(NetworkService networkService, ControlViewModel controlViewModel)
        {
            _controlViewModel = controlViewModel;
            _networkService = networkService;
            _videoService = new VideoStreamService();
            _videoService.FrameArrived += OnFrameArrived;
            _networkService.BBoxReceived += OnBBoxReceived;


            // ✅ 타임아웃 체크 타이머 (0.5초마다 검사)
            _bboxTimeoutTimer = new Timer(500);
            _bboxTimeoutTimer.Elapsed += (_, __) => CheckBBoxTimeout();
            _bboxTimeoutTimer.Start();
        }

        // Gstreamer 영상 수신
        private async void OnFrameArrived(BitmapSource frame)
        {
            CurrentFrame = frame;
            if (!_recorder.IsRecording)
                await _recorder.StartAsync(frame);

            await _recorder.PushFrameAsync(frame, BBoxes);
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
                for (int i = BBoxes.Count - 1; i >= 0; i--)
                {
                    if (!newIds.Contains(BBoxes[i].Id))
                        BBoxes.RemoveAt(i);
                }

                // ③ 새로 들어온/기존 객체 갱신
                foreach (var model in bboxData.Objects)
                {
                    var existing = BBoxes.FirstOrDefault(b => b.Id == model.Id);
                    if (existing != null)
                    {
                        // 좌표만 업데이트
                        existing.Update(model, frameWidth, frameHeight);
                    }
                    else
                    {
                        var bboxVm = new BBoxViewModel(model, frameWidth, frameHeight);
                        bboxVm.Clicked += OnBBoxClicked;
                        // 새 객체 추가
                        BBoxes.Add(bboxVm);
                    }
                }

                // ④ 선택된 객체 정보 갱신 (예시)
                SelectedBBoxInfo = $"탐지 객체 수: {BBoxes.Count}";
            });
        }

        // ✅ 일정 시간 미수신 시 Clear
        private void CheckBBoxTimeout()
        {
            if ((DateTime.UtcNow - _lastBBoxTime).TotalMilliseconds > 300)
            {
                App.Current.Dispatcher.Invoke(() => BBoxes.Clear());
            }
        }

        public void OnBBoxClicked(int id)
        {
            // 수동 모드일 때만 추적 명령 전송
            if (_controlViewModel.SelectedOperationMode == "수동")
            {
                _controlViewModel.TrackedTargetId = id;
                _ = _networkService.SendAsync(new Message<TrackTargetData>
                {
                    Type = "track_target",
                    Data = new TrackTargetData { TargetId = id }
                });
            }
        }
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
        }
        public void Dispose()
        {
            Stop();
        }

    }
}