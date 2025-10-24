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
using NetworkService = Operation_Control_System.Services.NetworkService;
using Task = System.Threading.Tasks.Task;

namespace Operation_Control_System.ViewModels
{
    public class VideoStreamViewModel : BaseViewModel
    {
        private readonly VideoStreamService _videoService;
        private readonly NetworkService _networkService;
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

        public VideoStreamViewModel(NetworkService networkService, ControlViewModel controlViewModel)
        {
            _controlViewModel = controlViewModel;
            _networkService = networkService;
            _videoService = new VideoStreamService();
            _videoService.FrameArrived += OnFrameArrived;
            _networkService.BBoxReceived += OnBBoxReceived;
        }

        // Gstreamer 영상 수신
        private void OnFrameArrived(BitmapSource frame)
        {
            CurrentFrame = frame;
        }

        // BBox 데이터 수신
        private void OnBBoxReceived(BBoxData bboxData)
        {
            if (CurrentFrame == null) return;

            int frameWidth = CurrentFrame.PixelWidth;
            int frameHeight = CurrentFrame.PixelHeight;

            App.Current.Dispatcher.Invoke(() =>
            {
                BBoxes.Clear();

                foreach (var model in bboxData.Objects)
                {
                    var vm = new BBoxViewModel(model, frameWidth, frameHeight);
                    BBoxes.Add(vm);
                }
            });
        }

        public void OnBBoxClicked(int id)
        {
            // 수동 모드일 때만 추적 명령 전송
            if (_controlViewModel.SelectedOperationMode == "수동")
            {
                _ = _networkService.SendAsync(new Message
                {
                    Type = "TrackTarget",
                    Data = new TrackTargetData(id)
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
        }

    }
}