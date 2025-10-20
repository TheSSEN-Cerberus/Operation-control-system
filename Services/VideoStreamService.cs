using Gst;
using Gst.App;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Operation_Control_System.Services
{
    public sealed class VideoStreamService : IDisposable
    {
        private Pipeline? _pipeline;
        private AppSink? _appSink;

        public event Action<BitmapSource>? FrameArrived;

        private static bool _gstInited;

        public VideoStreamService()
        {
            if (!_gstInited)
            {
                Gst.Application.Init();
                _gstInited = true;
            }
        }

        public void Start(int udpPort = 5600)
        {
            Stop();

            string caps = "application/x-rtp, media=video, encoding-name=H264, payload=96, clock-rate=90000";

            string pipelineDesc =
                $"udpsrc port={udpPort} caps=\"{caps}\" ! rtpjitterbuffer ! rtph264depay ! " +
                $"h264parse ! avdec_h264 ! videoconvert ! video/x-raw,format=BGR ! appsink name=appsink emit-signals=true sync=false max-buffers=1 drop=true";

            _pipeline = (Pipeline)Parse.Launch(pipelineDesc);
            _appSink = (AppSink)_pipeline.GetChildByName("appsink");

            // ✅ gstreamer-sharp-netcore 0.08은 이 형태로 이벤트 등록
            _appSink.NewSample += OnNewSample;

            _pipeline.SetState(State.Playing);
            Console.WriteLine("[GStreamer] Pipeline started.");
        }

        private void OnNewSample(object o, NewSampleArgs args)
        {
            var sink = (AppSink)o;
            var sample = sink.PullSample();
            if (sample == null)
                return;

            var buffer = sample.Buffer;
            var caps = sample.Caps;
            var s = caps.GetStructure(0);
            s.GetInt("width", out int width);
            s.GetInt("height", out int height);

            if (width <= 0 || height <= 0)
            {
                sample.Dispose();
                return;
            }

            buffer.Map(out MapInfo map, MapFlags.Read);
            try
            {
                if (map.Data == null || map.Data.Length == 0)
                    return;

                int stride = width * 3;
                int size = stride * height;

                // ✅ byte[] → unmanaged memory(IntPtr)
                IntPtr unmanagedPtr = Marshal.AllocHGlobal(size);
                Marshal.Copy(map.Data, 0, unmanagedPtr, size);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var bmp = BitmapSource.Create(
                            width, height, 96, 96,
                            System.Windows.Media.PixelFormats.Bgr24,
                            null,
                            unmanagedPtr,
                            size,
                            stride);

                        FrameArrived?.Invoke(bmp);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(unmanagedPtr);
                    }
                });
            }
            finally
            {
                buffer.Unmap(map);
                sample.Dispose();
            }
        }

        public void Stop()
        {
            try
            {
                if (_appSink != null)
                {
                    _appSink.NewSample -= OnNewSample;
                    _appSink = null;
                }
                if (_pipeline != null)
                {
                    _pipeline.SetState(State.Null);
                    _pipeline.Dispose();
                    _pipeline = null;
                }
            }
            catch { /* ignore */ }
        }

        public void Dispose() => Stop();
    }
}