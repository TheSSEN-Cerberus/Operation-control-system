using GLib;
using Gst;
using Gst.App;
using Operation_Control_System.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DateTime = System.DateTime;
using Debug = System.Diagnostics.Debug;
using Task = System.Threading.Tasks.Task;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// 실시간 MP4 인코딩 기반 비디오 레코더 (BBox 포함)
    /// </summary>
    public class VideoRecorderService : IDisposable
    {
        private Pipeline? _pipeline;
        private AppSrc? _appsrc;
        private bool _isRecording;
        private string _outputPath = "";
        private DateTime _startTime;

        private readonly string _recordDir;

        public bool IsRecording => _isRecording;

        public VideoRecorderService()
        {
            _recordDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Recordings");
            if (!Directory.Exists(_recordDir))
                Directory.CreateDirectory(_recordDir);
        }

        /// <summary>
        /// 녹화 시작 (첫 프레임 기준으로 파일명 결정)
        /// </summary>
        public async Task StartAsync(BitmapSource firstFrame)
        {
            if (_isRecording)
                return;

            _startTime = DateTime.Now;
            string fileName = $"{_startTime:yyyy-MM-dd_HH-mm-ss}.mp4";
            _outputPath = Path.Combine(_recordDir, fileName);
            _outputPath = _outputPath.Replace("\\", "/");  // 

            if (!Gst.Application.InitCheck())
                Gst.Application.Init();
            string pipelineDesc = $"appsrc name=src is-live=true format=time do-timestamp=true " +
                                  $"! videoconvert ! x264enc bitrate=4000 speed-preset=ultrafast tune=zerolatency " +
                                  $"! mp4mux ! filesink location=\"{_outputPath}\"";

            _pipeline = Parse.Launch(pipelineDesc) as Pipeline;
            _appsrc = _pipeline?.GetByName("src") as AppSrc;

            if (_appsrc == null || _pipeline == null)
                throw new Exception("Failed to create GStreamer pipeline for recording.");

            _appsrc.Caps = Caps.FromString(
                $"video/x-raw,format=BGRA,width={firstFrame.PixelWidth},height={firstFrame.PixelHeight},framerate=30/1");

            _pipeline.SetState(State.Playing);
            _isRecording = true;

            Debug.WriteLine($"[Recorder] ▶ Recording started: {_outputPath}");

            await Task.CompletedTask;
        }

        /// <summary>
        /// 프레임과 BBox를 합성하여 인코더에 전달
        /// </summary>
        public async Task PushFrameAsync(BitmapSource frame, IEnumerable<BBoxViewModel>? boxes = null)
        {
            if (!_isRecording || _appsrc == null || frame == null)
                return;

            try
            {
                var snapshot = boxes?.ToList();
                var merged = await Task.Run(() => RenderFrameWithBBoxes(frame, snapshot));

                int stride = merged.PixelWidth * (merged.Format.BitsPerPixel / 8);
                byte[] pixels = new byte[stride * merged.PixelHeight];
                merged.CopyPixels(pixels, stride, 0);
                using (var buf = new Gst.Buffer(pixels))
                {
                    _appsrc.PushBuffer(buf);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Recorder] PushFrame error: {ex.Message}");
            }
        }

        /// <summary>
        /// BBox가 포함된 프레임 렌더링
        /// </summary>
        private BitmapSource RenderFrameWithBBoxes(BitmapSource frame, IEnumerable<BBoxViewModel>? boxes)
        {
            if (boxes == null) return frame;

            int width = frame.PixelWidth;
            int height = frame.PixelHeight;

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawImage(frame, new Rect(0, 0, width, height));

                foreach (var box in boxes)
                {
                    var rect = new Rect(box.X, box.Y, box.Width, box.Height);
                    dc.DrawRectangle(null, new Pen(box.BoxColor, 2), rect);

                    FormattedText text = new FormattedText(
                        $"ID:{box.Id}",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        14,
                        Brushes.Yellow,
                        1.25);
                    dc.DrawText(text, new Point(box.X + 2, box.Y - 18));
                }
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>
        /// 녹화 중단
        /// </summary>
        public void Stop()
        {
            Debug.WriteLine("[VideoRecorder] Stop");
            if (!_isRecording)
                return;

            try
            {
                if (_appsrc != null)
                {
                    _appsrc.EndOfStream();

                    // ✅ EOS(End of Stream) 메시지 대기
                    var bus = _pipeline.Bus;
                    if (bus != null)
                    {
                        var msg = bus.TimedPopFiltered(
                            5 * Gst.Constants.SECOND, MessageType.Eos);
                        Debug.WriteLine("[VideoRecorder] EOS received: " + (msg != null));
                        msg?.Dispose();
                        bus.Dispose();
                    }
                }
                Task.Delay(500);
                _pipeline?.SetState(State.Null);
                _pipeline?.Dispose();
                _appsrc?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoRecorder] Stop error: {ex.Message}");
            }
        }
        public void Dispose() => Stop();
    }
}