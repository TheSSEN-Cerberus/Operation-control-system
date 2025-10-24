using Operation_Control_System.Models;
using System;
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
        public event Action<FireResultData>? FireResultReceived;

        // =====================
        // 생성자
        // =====================
        public NetworkService(int localPort = 50000)
        {
            _udp = new UDPDataLink(localPort);
            _udp.DataReceived += OnDataReceived;

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
        public async Task SendAsync(Message msg)
        {
            try
            {
                string json = JsonSerializer.Serialize(msg);
                await _udp.SendTextAsync(json);
                Console.WriteLine($"[Network] Sent: {msg.Type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Network] Send error: {ex.Message}");
            }
        }

        // =====================
        // 수신 콜백
        // =====================
        private void OnDataReceived(byte[] data, IPEndPoint sender)
        {
            string json = Encoding.UTF8.GetString(data);
            Console.WriteLine($"[Network] Received from {sender}: {json}");


            try
            {
                // 1️⃣ Data를 JsonElement로 받기 위한 임시 구조체로 역직렬화
                var msg = JsonSerializer.Deserialize<MessageTemp>(json);

                if (msg == null)
                {
                    Console.WriteLine("[Network] Invalid message structure.");
                    return;
                }

                // 2️⃣ Type 구분 후 개별 구조로 변환
                switch (msg.type)
                {

                    case "status":

                        var status = msg.data.Deserialize<StatusData>();
                        if (status != null)
                        {
                            _lastHeartbeat = DateTime.UtcNow;
                            StatusReceived?.Invoke(status);
                            ConnectionChanged?.Invoke(true);
                            Console.WriteLine($"[Network] Parsed StatusData RSSI={status.NetworkRssi}");
                        }
                        break;

                    case "fire_ready":
                        var ready = msg.data.Deserialize<FireReadyData>();
                        if (ready != null)
                        {
                            FireReadyReceived?.Invoke(ready);
                            Console.WriteLine("[Network] FireReady received.");
                        }
                        break;

                    case "bbox":
                        var bbox = msg.data.Deserialize<BBoxData>();
                        if (bbox != null)
                        {
                            BBoxReceived?.Invoke(bbox);
                            Console.WriteLine($"[Network] Received {bbox.Objects.Count} boxes.");
                        }
                        break;

                    case "fire_result":
                        var result = msg.data.Deserialize<FireResultData>();
                        if (result != null)
                        {
                            FireResultReceived?.Invoke(result);
                            Console.WriteLine($"[Network] FireResult: {(result.Success ? "HIT" : "MISS")}");
                        }
                        break;

                    default:
                        Console.WriteLine($"[Network] Unknown Type: {msg.type}");
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
            _ = StopAsync();
            _watchdog.Dispose();
        }
    }
}
