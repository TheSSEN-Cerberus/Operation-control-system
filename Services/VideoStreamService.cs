using Gst;
using Gst.App;
using OpenCvSharp;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Debug = System.Diagnostics.Debug;
using Size = OpenCvSharp.Size;
namespace Operation_Control_System.Services
{
    /// <summary>
    /// MainWindow 코드와 동일한 구조의 GStreamer 영상 수신 서비스
    /// DispatcherTimer 기반 GLib 루프 + AppSink (H.264 → BGRx)
    /// </summary>
    public sealed class VideoStreamService : IDisposable
    {

        private Mat? _prevGray = null;
        private Mat? _prevStabilized = null;
        private readonly object _cvLock = new();  // 스레드 안전

        private Pipeline? _pipeline;
        private AppSink? _appsink;
        private DispatcherTimer? _glibTimer;

        private static bool _gstInitialized;
        private bool _isRunning = false;

        private int _frameCount = 0;
        private Stopwatch _fpsTimer = new();

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

            // H264
            string pipelineDesc =
            $"udpsrc port={udpPort} caps=\"application/x-rtp, media=video, encoding-name=H264, payload=96\" ! " +
            "rtph264depay ! " +
            "h264parse ! avdec_h264 ! " +   // 또는 avdec_h264 대신 videotestdec 등 사용 가능
            "videoconvert ! video/x-raw,format=BGRx ! " +
            "appsink name=sink emit-signals=true max-buffers=1 drop=true sync=false";
            // ver 1
            //string pipelineDesc =
            //    $"udpsrc port={udpPort} " +
            //    "caps=application/x-rtp,media=video,encoding-name=JPEG,payload=26,clock-rate=90000 ! " +
            //    "rtpjpegdepay ! jpegdec ! videoconvert ! " +
            //    "video/x-raw,format=BGRx ! appsink name=sink emit-signals=true max-buffers=1 drop=true";

            // ver2
            //string pipelineDesc =
            //    $"udpsrc port={udpPort} buffer-size=4096 ! "
            //  + "caps=application/x-rtp,media=video,encoding-name=JPEG,payload=26,clock-rate=90000 ! "
            //  + "rtpjpegdepay latency=0 ! jpegdec ! videoconvert ! "
            //  + "video/x-raw,format=BGRx ! "
            //  + "appsink name=sink emit-signals=true max-buffers=1 drop=true sync=false";


            // 테스트 용
            //        string pipelineDesc =
            //$"udpsrc port={udpPort} " +
            //"caps=application/x-rtp,media=video,encoding-name=H264,payload=96,clock-rate=90000 ! " +
            //"rtph264depay ! queue ! decodebin ! queue ! videoconvert ! queue ! " +
            //"video/x-raw,format=BGRx ! appsink name=sink emit-signals=true max-buffers=3 drop=true";


            try
            {
                _pipeline = Parse.Launch(pipelineDesc) as Pipeline;
                var sinkElement = _pipeline.GetChildByName("sink");
                _appsink = new AppSink(sinkElement.Handle);
                _appsink.EmitSignals = true;
                _appsink.NewSample += OnNewSample;

                _pipeline.SetState(State.Playing);
                StartGlibLoop();

                // ✅ FPS 타이머 시작
                _fpsTimer.Restart();
                _frameCount = 0;

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

        // 안정화 X 버전
        private void OnNewSample(object sender, EventArgs args)
        {
            //System.Diagnostics.Debug.WriteLine($"이미지 수신중");
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
            // ✅ FPS 계산 및 로그 출력
            _frameCount++;
            double elapsed = _fpsTimer.Elapsed.TotalSeconds;
            if (elapsed >= 1.0)
            {
                double fps = _frameCount / elapsed;
                Debug.WriteLine($"[VideoStream] FPS: {fps:F1}");
                _fpsTimer.Restart();
                _frameCount = 0;
            }
        }
        /// <summary>
        /// [4] AppSink 프레임 수신 콜백 (안정화 버전)
        /// </summary>
   



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