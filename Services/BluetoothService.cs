using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace Operation_Control_System.Services
{
    public class BluetoothService : IDisposable
    {
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _commandCharacteristic;
        private CancellationTokenSource? _reconnectCts;
        private readonly System.Timers.Timer _idleTimer;

        private readonly string _deviceName;
        private readonly Guid _cmdUuid;
        private readonly ushort _commandHandle;
        private readonly string _macAddress;

        public string MoveCommand { get; }
        public string StopCommand { get; }
        private string? LastCommand;

        private DateTime _lastCommandTime = DateTime.MinValue;

        //public bool IsConnected =>
        //    _device?.ConnectionStatus == BluetoothConnectionStatus.Connected &&
        //    _commandCharacteristic != null;

        public bool IsConnected => _commandCharacteristic != null;

        // ✅ 연결 상태 변경 이벤트
        public event Action<bool>? ConnectionChanged;

        public BluetoothService()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            var config = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            var section = config.GetSection("BluetoothConfig");
            if (!section.Exists())
                throw new Exception("[BLE] ⚠ BluetoothConfig section not found in appsettings.json");

            _deviceName = section["DeviceName"] ?? throw new Exception("[BLE] DeviceName missing");
            _macAddress = section["MacAddress"] ?? throw new Exception("[BLE] MacAddress missing");
            _cmdUuid = Guid.Parse(section["CmdUUID"] ?? throw new Exception("[BLE] CmdUUID missing"));
            _commandHandle = Convert.ToUInt16(section["CommandCharacteristicHandle"] ?? "0x0", 16);

            MoveCommand = section.GetSection("Commands")["Move"] ?? "";
            StopCommand = section.GetSection("Commands")["Stop"] ?? "";

            Debug.WriteLine($"[BLE] ✅ Config loaded → {_deviceName}, Handle=0x{_commandHandle:X4}");

            // ✅ Idle Timer: 1초마다 체크
            _idleTimer = new System.Timers.Timer(1000);
            _idleTimer.Elapsed += CheckIdleTimeout;
            _idleTimer.AutoReset = true;
            _idleTimer.Start();
        }

        private void CheckIdleTimeout(object? sender, ElapsedEventArgs e)
        {
            if (!IsConnected) return;
            if (_lastCommandTime == DateTime.MinValue) return;
            if (LastCommand == MoveCommand) return;

            double idleSec = (DateTime.Now - _lastCommandTime).TotalSeconds;
            if (idleSec >= 10)
            {
                Debug.WriteLine($"[BLE] ⏱ Idle for {idleSec:F1}s → Sending Stop command");
                _ = SendCommandAsync(StopCommand);
                _lastCommandTime = DateTime.Now; // reset timer after sending stop
            }
        }

        // ✅ 연결 재시도 루프
        public async Task StartAutoConnectAsync(int retryDelayMs = 3000)
        {
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected)
                    {
                        Debug.WriteLine("[BLE] 🔄 Trying to connect...");
                        bool ok = await ConnectAsync();
                        if (ok)
                        {
                            Debug.WriteLine("[BLE] ✅ Connected to BLE device.");
                            ConnectionChanged?.Invoke(true);
                        }
                        else
                        {
                            Debug.WriteLine("[BLE] ❌ Connection failed, retrying...");
                            ConnectionChanged?.Invoke(false);
                        }
                    }
                    await Task.Delay(retryDelayMs, token);
                }
            }, token);
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _device?.Dispose();
                await Task.Delay(500);
                _device = null;
                _commandCharacteristic = null;

                ulong address = ConvertMacToULong(_macAddress);
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

                if (_device == null)
                {
                    Debug.WriteLine("[BLE] ❌ Device not found.");
                    return false;
                }

                await Task.Delay(1000);
                var services = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                foreach (var service in services.Services)
                {
                    var characteristics = await service.GetCharacteristicsAsync();
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        if (characteristic.Uuid == _cmdUuid)
                        {
                            Debug.WriteLine("[BLE] Success Connect");
                            _commandCharacteristic = characteristic;
                            _lastCommandTime = DateTime.Now; // reset timer
                            return true;
                        }
                    }
                }

                Debug.WriteLine("[BLE] ❌ Command characteristic not found.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BLE] ❌ Connect error: {ex.Message}");
                return false;
            }
        }

        public async Task SendCommandAsync(string hexCommand)
        {
            if (_commandCharacteristic == null)
            {
                Debug.WriteLine("[BLE] ⚠ Not connected to characteristic.");
                ConnectionChanged?.Invoke(false);
                return;
            }

            try
            {
                byte[] bytes = ConvertHexToBytes(hexCommand);
                using var writer = new DataWriter();
                writer.WriteBytes(bytes);
                var status = await _commandCharacteristic.WriteValueAsync(writer.DetachBuffer());
                Debug.WriteLine($"[BLE] → Sent {hexCommand} (Status: {status})");
                LastCommand = hexCommand;

                // ✅ 마지막 명령 시간 갱신
                _lastCommandTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BLE] ❌ Send error: {ex.Message}");
                ConnectionChanged?.Invoke(false);
                _commandCharacteristic = null; // 재연결 유도
            }
        }

        private static byte[] ConvertHexToBytes(string hex)
        {
            int len = hex.Length;
            byte[] bytes = new byte[len / 2];
            for (int i = 0; i < len; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private static ulong ConvertMacToULong(string mac)
        {
            var bytes = mac.Split(':');
            ulong result = 0;
            foreach (var b in bytes)
                result = (result << 8) + Convert.ToByte(b, 16);
            return result;
        }

        public void Dispose()
        {
            _idleTimer?.Stop();
            _reconnectCts?.Cancel();
            _commandCharacteristic = null;
            _device?.Dispose();
            _device = null;
        }
    }
}
