using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// 순수 UDP 송수신 담당 (프로토콜 파싱은 상위 서비스에서 처리)
    /// </summary>
    public class UDPDataLink : IDisposable
    {

        // 설정 (필요 시 App 설정으로 교체 가능)
        public int LocalPort { get; }
        public string RemoteIP { get; set; } = "";
        public int RemotePort { get; set; } = 5000;

        // 이벤트
        public event Action<byte[], IPEndPoint>? DataReceived;

        // 내부 상태
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;

        public UDPDataLink(int localPort)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var section = config.GetSection("NetworkConfig");

            //RemoteIP = section["RemoteIP"] ?? "127.0.0.1";
            RemotePort = int.Parse(section["RemotePort"] ?? "50000");
            LocalPort = int.Parse(section["LocalPort"] ?? "50000");
        }

        /// <summary>
        /// 수신 시작 (0.0.0.0:LocalPort 바인딩)
        /// </summary>
        public async Task StartUDPAsync()
        {
            if (_udp != null) return;

            _cts = new CancellationTokenSource();

            // 바인딩
            var localEP = new IPEndPoint(IPAddress.Any, LocalPort);
            _udp = new UdpClient(localEP);

            // 수신 루프 시작
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
            await Task.CompletedTask;
        }

        /// <summary>
        /// 수신 루프 - 비동기
        /// </summary>
        private async Task ReceiveLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _udp != null)
                {
                    var result = await _udp.ReceiveAsync(token);
                    if (string.IsNullOrWhiteSpace(RemoteIP) || RemoteIP == "0.0.0.0")
                    {
                        Debug.WriteLine(RemoteIP);
                        Debug.WriteLine(RemotePort);

                        RemoteIP = result.RemoteEndPoint.Address.ToString();
                        RemotePort = result.RemoteEndPoint.Port; // 필요 시 포트도 같이 고정
                        System.Diagnostics.Debug.WriteLine($"[UDP] Remote IP fixed: {RemoteIP}:{RemotePort}");
                    }
                    DataReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (ObjectDisposedException)
            {
                // 소켓 해제 시
            }
            catch (Exception ex)
            {
                // TODO: 로깅 (필요시)
                System.Diagnostics.Debug.WriteLine($"UDP receive error: {ex.Message}");
            }
        }

        /// <summary>
        /// 송신 (바이트 배열 그대로)
        /// </summary>
        public async Task SendAsync(byte[] payload)
        {
            if (_udp == null) throw new InvalidOperationException("UDP not started.");
            if (string.IsNullOrWhiteSpace(RemoteIP)) throw new ArgumentException("RemoteIP not set.");

            await _udp.SendAsync(payload, payload.Length, RemoteIP, RemotePort);
        }

        /// <summary>
        /// 송신 (문자열 → UTF8)
        /// </summary>
        public Task SendTextAsync(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return SendAsync(bytes);
        }

        /// <summary>
        /// 종료
        /// </summary>
        public async Task StopUDPAsync()
        {
            _cts?.Cancel();

            try { _udp?.Close(); } catch { /* ignore */ }
            _udp?.Dispose();
            _udp = null;

            _cts?.Dispose();
            _cts = null;

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = StopUDPAsync();
        }
    }
}
