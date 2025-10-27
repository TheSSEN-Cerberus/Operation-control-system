using Gst;
using Gst.App;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// MainWindow 코드와 동일한 구조의 GStreamer 영상 수신 서비스
    /// DispatcherTimer 기반 GLib 루프 + AppSink (H.264 → BGRx)
    /// </summary>
    public sealed class VideoStreamService : IDisposable
    {
        private Pipeline? _pipeline;
        private AppSink? _appsink;
        private DispatcherTimer? _glibTimer;

        private static bool _gstInitialized;
        private bool _isRunning = false;

        public event Action<BitmapSource>? FrameArrived;

        public VideoStreamService()
        {
            InitGStreamer();
        }

        /// <summary>
        /// [1] GStreamer 환경변수 및 초기화 (MainWindow 코드와 동일)
        /// </summary>
        private void InitGStreamer()
        {
            if (_gstInitialized) return;

            string gstRoot = @"C:\gstreamer\1.0\mingw_x86_64";
            Environment.SetEnvironmentVariable("PATH",
                $"{gstRoot}\\bin;" + Environment.GetEnvironmentVariable("PATH"));
            Environment.SetEnvironmentVariable("GST_PLUGIN_SYSTEM_PATH_1_0",
                $@"{gstRoot}\lib\gstreamer-1.0");

            Gst.Application.Init();
            _gstInitialized = true;
            System.Diagnostics.Debug.WriteLine("[GStreamer] ✅ Initialized (Service)");
        }

        /// <summary>
        /// [2] 영상 수신 시작
        /// </summary>
        public void Start(int udpPort)
        {
            if (_isRunning) return;
            Stop();
            _isRunning = true;

            //string pipelineDesc =
            //    $"udpsrc port={udpPort} " +
            //    "caps=application/x-rtp,media=video,encoding-name=JPEG,payload=26,clock-rate=90000 ! " +
            //    "rtpjpegdepay ! jpegdec ! videoconvert ! " +
            //    "video/x-raw,format=BGRx ! appsink name=sink emit-signals=true max-buffers=1 drop=true";
            string pipelineDesc =
    $"udpsrc port={udpPort} " +
    "caps=application/x-rtp,media=video,encoding-name=H264,payload=96,clock-rate=90000 ! " +
    "rtph264depay ! queue ! decodebin ! queue ! videoconvert ! queue ! " +
    "video/x-raw,format=BGRx ! appsink name=sink emit-signals=true max-buffers=5 drop=false";


            try
            {
                _pipeline = Parse.Launch(pipelineDesc) as Pipeline;
                var sinkElement = _pipeline.GetChildByName("sink");
                _appsink = new AppSink(sinkElement.Handle);
                _appsink.EmitSignals = true;
                _appsink.NewSample += OnNewSample;

                _pipeline.SetState(State.Playing);
                StartGlibLoop();

                System.Diagnostics.Debug.WriteLine($"[GStreamer] ▶️ Pipeline started (UDP {udpPort})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GStreamer ERROR] {ex}");
                Stop();
            }
        }

        /// <summary>
        /// [3] GLib 이벤트 루프 유지 (MainWindow 동일)
        /// </summary>
        private void StartGlibLoop()
        {
            _glibTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _glibTimer.Tick += (s, e) => GLib.MainContext.Iteration(false);
            _glibTimer.Start();
        }

        /// <summary>
        /// [4] AppSink 프레임 수신 콜백
        /// </summary>
        private void OnNewSample(object sender, EventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"이미지 수신중");
            using var sample = _appsink!.PullSample();
            if (sample == null) return;

            var caps = sample.Caps;
            var s = caps.GetStructure(0);
            int width = (int)s.GetValue("width").Val;
            int height = (int)s.GetValue("height").Val;

            if (!sample.Buffer.Map(out MapInfo map, MapFlags.Read))
                return;

            try
            {
                // ✅ 프레임 복사 (UI 접근용)
                byte[] frameCopy = new byte[map.Data.Length];
                System.Buffer.BlockCopy(map.Data, 0, frameCopy, 0, frameCopy.Length);

                int stride = width * 4;

                // ✅ UI 스레드로 안전하게 전달
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    var bmp = BitmapSource.Create(
                        width, height, 96, 96,
                        System.Windows.Media.PixelFormats.Bgr32,
                        null,
                        frameCopy,
                        stride);
                    bmp.Freeze(); // MVVM에서도 안전히 전달 가능
                    FrameArrived?.Invoke(bmp);
                });
            }
            finally
            {
                sample.Buffer.Unmap(map);
            }
        }

        /// <summary>
        /// [5] 정지 및 해제
        /// </summary>
        public void Stop()
        {
            try
            {
                _isRunning = false;

                if (_appsink != null)
                {
                    _appsink.NewSample -= OnNewSample;
                    _appsink = null;
                }

                if (_pipeline != null)
                {
                    _pipeline.SetState(State.Null);
                    _pipeline.Dispose();
                    _pipeline = null;
                }

                _glibTimer?.Stop();
                _glibTimer = null;

                System.Diagnostics.Debug.WriteLine("[GStreamer] 🛑 Pipeline stopped.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Stop ERROR] {ex.Message}");
            }
        }

        public void Dispose() => Stop();
    }
}