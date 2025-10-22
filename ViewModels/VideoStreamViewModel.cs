using Operation_Control_System.Infrastructure;
using Operation_Control_System.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Operation_Control_System.ViewModels
{
    public class VideoStreamViewModel : BaseViewModel
    {
        private readonly VideoStreamService _videoService;
        private readonly object _lock = new();
        private bool _started = false;

        private ImageSource? _currentFrame;
        public ImageSource? CurrentFrame
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

        public VideoStreamViewModel()
        {
            _videoService = new VideoStreamService();
            _videoService.FrameArrived += OnFrameArrived;
        }

        private void OnFrameArrived(BitmapSource frame)
        {
            CurrentFrame = frame;
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