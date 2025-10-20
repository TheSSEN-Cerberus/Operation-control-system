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
    public sealed class VideoStreamService : IDisposable
    {
        private Pipeline? _pipeline;
        private AppSink? _sink;
        private static bool _gstInitialized;

        public event Action<BitmapSource>? FrameArrived;

        public VideoStreamService()
        {
            Console.WriteLine("hello");
            Environment.SetEnvironmentVariable("GST_DEBUG", "3");
            if (!_gstInitialized)
            {
                GstApp.Init();
                _gstInitialized = true;
                Console.WriteLine("[GStreamer] Initialized.");
            }
        }

        public void Start(int udpPort = 5601)
        {
            Stop();

            try
            {
                _pipeline = Parse.Launch("videotestsrc is-live=true ! videoconvert ! video/x-raw,format=BGR ! appsink name=sink emit-signals=true sync=false") as Pipeline;

                if (_pipeline == null)
                {
                    Console.WriteLine("[GStreamer] ❌ Parse.Launch failed. Check pipeline string.");
                }
                else
                {
                    Console.WriteLine("[GStreamer] ✅ Pipeline created successfully.");
                }

                _sink.NewSample += OnNewSample;
                _pipeline.SetState(State.Playing);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoStream ERROR] {ex.Message}");
            }
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

            if (buffer.Map(out MapInfo map, MapFlags.Read))
            {
                try
                {
                    int stride = width * 3;
                    IntPtr unmanaged = Marshal.AllocHGlobal((int)map.Size);
                    Marshal.Copy(map.Data, 0, unmanaged, (int)map.Size);

                    WpfApp.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var bmp = BitmapSource.Create(
                                width, height, 96, 96,
                                System.Windows.Media.PixelFormats.Bgr24,
                                null,
                                unmanaged,
                                (int)map.Size,
                                stride);
                            FrameArrived?.Invoke(bmp);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(unmanaged);
                        }
                    });
                }
                finally
                {
                    buffer.Unmap(map);
                }
            }

            sample.Dispose();
        }

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

                Console.WriteLine("[GStreamer] Pipeline stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stop ERROR] {ex.Message}");
            }
        }

        public void Dispose() => Stop();
    }
}
