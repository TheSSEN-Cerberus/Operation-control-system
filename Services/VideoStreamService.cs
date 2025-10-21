using Gst;
using Gst.App;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using GstApp = Gst.Application;
using WpfApp = System.Windows.Application;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// GStreamer 기반 영상 수신 및 프레임 전달 서비스 (.NET 8 안정화 버전)
    /// </summary>
    public sealed class VideoStreamService : IDisposable
    {
        private Pipeline? _pipeline;
        private AppSink? _sink;
        private static bool _gstInitialized;

        public event Action<BitmapSource>? FrameArrived;

        public VideoStreamService()
        {
            //if (!_gstInitialized)
            //{
            //    GstApp.Init();
            //    _gstInitialized = true;
            //    System.Diagnostics.Debug.WriteLine("[GStreamer] ✅ Initialized (.NET 8)");
            //    Console.WriteLine("[GStreamer] ✅ Initialized (.NET 8)");
            //}

            if (!_gstInitialized)
            {
                // ✅ GStreamer 플러그인 경로를 명시적으로 지정
                Environment.SetEnvironmentVariable(
                    "GST_PLUGIN_PATH",
                    @"C:\Program Files\gstreamer\1.0\x86_64\lib\gstreamer-1.0"
                );
                Environment.SetEnvironmentVariable(
                    "PATH",
                    Environment.GetEnvironmentVariable("PATH") + @";C:\Program Files\gstreamer\1.0\msvc_x86_64\bin"
                );

                Environment.SetEnvironmentVariable("GST_DEBUG", "3");

                GstApp.Init();
                _gstInitialized = true;
                Console.WriteLine("[GStreamer] ✅ Initialized (.NET 8)");
            }
        }

        /// <summary>
        /// 영상 스트림 시작 (기본: videotestsrc, 실제 사용 시 udpsrc 파이프라인으로 변경 가능)
        /// </summary>
        public void Start(int udpPort = 5600, bool useUdp = false)
        {
            Stop();

            try
            {
                string pipelineDesc = $"udpsrc port={udpPort} " +
                    "caps=\"application/x-rtp, media=video, encoding-name=H264, payload=96, clock-rate=90000\" " +
                    "! rtpjitterbuffer latency=100 " +
                    "! rtph264depay " +
                    "! decodebin " +
                    "! videoconvert " +
                    "! video/x-raw,format=BGR " +
                    "! appsink name=sink emit-signals=true sync=false";

                var element = Parse.Launch(pipelineDesc);
                if (element == null)
                {
                    Console.WriteLine("[GStreamer] ❌ Parse.Launch returned null. Check PATH or pipeline string.");
                    return;
                }

                // 파이프라인 구성
                _pipeline = element as Pipeline ?? new Pipeline("main-pipeline");
                if (_pipeline != element)
                    _pipeline.Add(element);

                // appsink 가져오기
                _sink = _pipeline.GetChildByName("sink") as AppSink;
                if (_sink == null)
                {
                    Console.WriteLine("[GStreamer] ❌ AppSink not found.");
                    return;
                }

                // 이벤트 핸들러 연결
                _sink.NewSample += OnNewSample;

                // 파이프라인 실행
                _pipeline.SetState(State.Playing);
                Console.WriteLine($"[GStreamer] ▶️ Pipeline started. (useUdp={useUdp})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoStream ERROR] {ex.Message}");
            }
        }

        /// <summary>
        /// GStreamer AppSink의 새 프레임 수신 이벤트
        /// </summary>
        private void OnNewSample(object sender, NewSampleArgs args)
        {
            System.Diagnostics.Debug.WriteLine("이미지 수신");
            var sink = (AppSink)sender;
            using var sample = sink.PullSample();
            if (sample == null) return;

            var caps = sample.Caps;
            var s = caps.GetStructure(0);
            s.GetInt("width", out int width);
            s.GetInt("height", out int height);
            if (width <= 0 || height <= 0) return;

            var buffer = sample.Buffer;
            if (!buffer.Map(out MapInfo map, MapFlags.Read)) return;

            try
            {
                int stride = width * 3;
                byte[] managedBuffer = map.Data;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var bmp = System.Windows.Media.Imaging.BitmapSource.Create(
                        width, height, 96, 96,
                        System.Windows.Media.PixelFormats.Bgr24,
                        null,
                        managedBuffer,
                        stride
                    );
                    FrameArrived?.Invoke(bmp);
                });
            }
            finally
            {
                buffer.Unmap(map);
            }

        }

        /// <summary>
        /// 파이프라인 중지 및 자원 해제
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_sink != null)
                {
                    _sink.NewSample -= OnNewSample;
                    _sink = null;
                }

                if (_pipeline != null)
                {
                    _pipeline.SetState(State.Null);
                    _pipeline.Dispose();
                    _pipeline = null;
                }

                Console.WriteLine("[GStreamer] 🛑 Pipeline stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stop ERROR] {ex.Message}");
            }
        }

        public void Dispose() => Stop();
    }
}