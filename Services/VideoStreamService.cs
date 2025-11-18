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

            // ver 1
            string pipelineDesc =
                $"udpsrc port={udpPort} " +
                "caps=application/x-rtp,media=video,encoding-name=JPEG,payload=26,clock-rate=90000 ! " +
                "rtpjpegdepay ! jpegdec ! videoconvert ! " +
                "video/x-raw,format=BGRx ! appsink name=sink emit-signals=true max-buffers=1 drop=true";

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

        /// <summary>
        /// [4] AppSink 프레임 수신 콜백
        /// </summary>
        private void OnNewSample(object sender, EventArgs args)
        {
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
                // ---- [1] GStreamer BGRx 데이터 → byte[] ----
                byte[] frameCopy = new byte[map.Data.Length];
                System.Buffer.BlockCopy(map.Data, 0, frameCopy, 0, frameCopy.Length);

                // ---- [2] byte[] → Mat(BGRA) (OpenCvSharp 4.11 방식) ----
                Mat matBGRA = new Mat(height, width, MatType.CV_8UC4);
                System.Runtime.InteropServices.Marshal.Copy(
                    frameCopy, 0, matBGRA.Data, frameCopy.Length
                );

                // ---- [3] BGRA → BGR ----
                Mat matBGR = new Mat();
                Cv2.CvtColor(matBGRA, matBGR, ColorConversionCodes.BGRA2BGR);

                // ---- [4] 영상 안정화 ----
                Mat stabilizedBGR;
                lock (_cvLock)
                {
                    stabilizedBGR = StabilizeFrame(matBGR);
                }

                // ---- [5] BGR → BGRA ----
                Mat stabilizedBGRA = new Mat();
                Cv2.CvtColor(stabilizedBGR, stabilizedBGRA, ColorConversionCodes.BGR2BGRA);

                // ---- [6] WPF BitmapSource 생성 ----
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    // Mat → byte[] 변환
                    int dataSize = stabilizedBGRA.Rows * stabilizedBGRA.Cols * stabilizedBGRA.ElemSize();
                    byte[] outBytes = new byte[dataSize];
                    System.Runtime.InteropServices.Marshal.Copy(
                        stabilizedBGRA.Data,
                        outBytes,
                        0,
                        dataSize
                    );

                    // stride = width * 4 (BGRA)
                    int stride = width * 4;

                    var bmp = BitmapSource.Create(
                        width, height, 96, 96,
                        PixelFormats.Bgra32,
                        null,
                        outBytes,
                        stride
                    );
                    bmp.Freeze();
                    FrameArrived?.Invoke(bmp);
                });
            }
            finally
            {
                sample.Buffer.Unmap(map);
            }

            // FPS 계산
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

        /// <summary>
        /// Optical Flow 기반 간단 영상 흔들림 보정
        /// </summary>
        private Mat StabilizeFrame(Mat frame)
        {
            Mat gray = new();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

            // 최초 프레임이면 초기화
            if (_prevGray == null)
            {
                _prevGray = gray.Clone();
                return frame;
            }

            // ① 특징점 추출 (4.11에서는 mask 포함 8개 인수 필요)
            Point2f[] prevPts = Cv2.GoodFeaturesToTrack(
                _prevGray,
                200,       // maxCorners
                0.01,      // qualityLevel
                30,        // minDistance
                null,      // mask
                3,         // blockSize
                false,     // useHarris
                0.04       // k
            );

            if (prevPts == null || prevPts.Length == 0)
            {
                _prevGray = gray.Clone();
                return frame;
            }

            // ② Optical Flow 계산
            Mat nextPtsMat = new Mat();
            Mat statusMat = new Mat();
            Mat errMat = new Mat();

            Cv2.CalcOpticalFlowPyrLK(
                InputArray.Create(_prevGray),
                InputArray.Create(gray),
                InputArray.Create(prevPts),
                (InputOutputArray)OutputArray.Create(nextPtsMat),
                OutputArray.Create(statusMat),
                OutputArray.Create(errMat),
                winSize: new Size(21, 21),
                maxLevel: 3
            );

            // ③ Mat → 배열 변환 (4.11에서는 GetArray<T>() 사용 가능)
            nextPtsMat.GetArray<Point2f>(out Point2f[] nextPts);
            statusMat.GetArray<byte>(out byte[] status);
            errMat.GetArray<float>(out float[] err);

            // 좋은 점만 필터링
            List<Point2f> goodPrev = new();
            List<Point2f> goodNext = new();

            for (int i = 0; i < status.Length; i++)
            {
                if (status[i] == 1)
                {
                    goodPrev.Add(prevPts[i]);
                    goodNext.Add(nextPts[i]);
                }
            }

            if (goodPrev.Count < 10)
            {
                _prevGray = gray.Clone();
                return frame;
            }

            // ④ Affine Transform 계산
            Mat transform = Cv2.EstimateAffine2D(
                InputArray.Create(goodPrev.ToArray()),
                InputArray.Create(goodNext.ToArray())
            );

            if (transform.Empty())
            {
                _prevGray = gray.Clone();
                return frame;
            }

            // ⑤ 흔들림 보정 적용
            Mat stabilized = new Mat();
            Cv2.WarpAffine(
                frame,
                stabilized,
                transform,
                frame.Size(),
                InterpolationFlags.Linear,
                BorderTypes.Reflect101
            );

            _prevGray = gray.Clone();
            return stabilized;
        }




        public void Dispose() => Stop();
    }
}