using Operation_Control_System.Models;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Interop;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// CSU 통신 프로토콜 기반 네트워크 서비스 (안정화 버전)
    /// UDP 수신/송신, 하트비트 감시, 메시지 파싱 담당
    /// </summary>
    public class NetworkService : IDisposable
    {
        private readonly UDPDataLink _udp;
        private readonly LoggerService _logger;
        private readonly System.Timers.Timer _watchdog;
        private DateTime _lastHeartbeat = DateTime.MinValue;
        private bool _isConnected;

        // =====================
        // 이벤트 정의
        // =====================
        public event Action<bool>? ConnectionChanged;
        public event Action<StatusData>? StatusReceived;
        public event Action<FireReadyData>? FireReadyReceived;
        public event Action<BBoxData>? BBoxReceived;
        public event Action<TrackTargetData>? TrackTargetReceived;
        public event Action<FireDoneData>? FireDoneReceived;

        // =====================
        // 생성자
        // =====================
        public NetworkService(int localPort = 50000)
        {
            _udp = new UDPDataLink(localPort);
            _udp.DataReceived += OnDataReceived;

            _logger = new LoggerService();

            _watchdog = new System.Timers.Timer(1000); // 1초마다 연결상태 점검
            _watchdog.Elapsed += (_, __) => CheckConnection();
        }

        // =====================
        // 서비스 시작 / 중지
        // =====================
        public async Task StartAsync()
        {
            await _udp.StartUDPAsync();
            _watchdog.Start();
            Console.WriteLine("[Network] UDP listening started.");
        }

        public async Task StopAsync()
        {
            _watchdog.Stop();
            await _udp.StopUDPAsync();
            Console.WriteLine("[Network] Stopped.");
        }

        // =====================
        // 송신 (Message 객체)
        // =====================
        public async Task SendAsync<T>(Message<T> msg)
        {
            if (_udp.RemoteIP == null) return;
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(msg, options);

                await _udp.SendTextAsync(json);
                _logger.Log("TX", msg.Type, _udp.RemoteIP, _udp.RemotePort, json);
                Debug.WriteLine($"[Network] Sent: {msg.Type}");

            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"[Network] Send error: {ex}");
            }
        }


        // =====================
        // 수신 콜백
        // =====================
        private void OnDataReceived(byte[] data, IPEndPoint sender)
        {
            string json = Encoding.UTF8.GetString(data);
            //Debug.WriteLine($"[Network] Received from {sender}: {json}");

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp))
                {
                    Console.WriteLine("[Network] Missing 'type' field.");
                    return;
                }
                string type = typeProp.GetString() ?? "";

                _logger.Log("RX", type, sender.Address.ToString(), sender.Port, json);
                switch (type)
                {
                    case "status":
                        var statusMsg = JsonSerializer.Deserialize<Message<StatusData>>(json);
                        if (statusMsg?.Data != null)
                        {
                            _lastHeartbeat = DateTime.UtcNow;
                            StatusReceived?.Invoke(statusMsg.Data);
                            ConnectionChanged?.Invoke(true);
                        }
                        break;

                    case "fire_ready":
                        Debug.WriteLine(json);
                        var readyMsg = JsonSerializer.Deserialize<Message<FireReadyData>>(json);
                        if (readyMsg?.Data != null)
                        {
                            FireReadyReceived?.Invoke(readyMsg.Data);
                            Console.WriteLine("[Network] FireReady received.");
                        }
                        break;

                    case "bbox":
                        var bboxMsg = JsonSerializer.Deserialize<Message<BBoxData>>(json);
                        if (bboxMsg?.Data != null)
                        {
                            BBoxReceived?.Invoke(bboxMsg.Data);
                            Console.WriteLine($"[Network] Received {bboxMsg.Data.Objects.Count} boxes.");
                        }
                        break;

                    case "track_target":
                        var trackMsg = JsonSerializer.Deserialize<Message<TrackTargetData>>(json);
                        if (trackMsg?.Data != null)
                        {
                            TrackTargetReceived?.Invoke(trackMsg.Data);
                            Debug.WriteLine($"[Network] TrackTarget Received (ID={trackMsg.Data})");
                        }
                        break;
                    case "fire_done":
                        var fireDoneMsg = JsonSerializer.Deserialize<Message<FireDoneData>>(json);
                        if (fireDoneMsg?.Data != null)
                        {
                            FireDoneReceived?.Invoke(fireDoneMsg.Data);
                            Debug.WriteLine($"[Network] FireDoneReceived (ID={fireDoneMsg.Data})");
                        }
                        break;

                    default:
                        Console.WriteLine($"[Network] Unknown Type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Network] Parse error: {ex.Message}");
            }
        }


        // =====================
        // 연결상태 감시
        // =====================
        private void CheckConnection()
        {
            bool connected = (DateTime.UtcNow - _lastHeartbeat).TotalSeconds <= 3;
            if (connected != _isConnected)
            {
                _isConnected = connected;
                ConnectionChanged?.Invoke(_isConnected);
                Console.WriteLine($"[Network] Connection: {(_isConnected ? "Active" : "Lost")}");
            }
        }

        // =====================
        // 임시 구조체 (JsonElement 파싱용)
        // =====================
        private class MessageTemp
        {
            public string type { get; set; } = "";
            public string timeStamp { get; set; } = "";
            public JsonElement data { get; set; }
        }

        // =====================
        // 리소스 해제
        // =====================
        public void Dispose()
        {
            ConnectionChanged = null;
            StatusReceived = null;
            FireReadyReceived = null;
            BBoxReceived = null;
            TrackTargetReceived = null;
            _ = StopAsync();
            _watchdog.Dispose();
        }
    }
}
