using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Operation_Control_System.Services
{
    public class BluetoothService : IDisposable
    {
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _commandCharacteristic;
        private CancellationTokenSource? _reconnectCts;

        private readonly string _deviceName;
        private readonly Guid _cmdUuid;
        private readonly ushort _commandHandle;
        private readonly string _macAddress;
        private BluetoothConnectionStatus _lastStatus;

        public string MoveCommand { get; }
        public string StopCommand { get; }

        public bool IsConnected =>
        _device?.ConnectionStatus == BluetoothConnectionStatus.Connected &&
        _commandCharacteristic != null;


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
        }

        // ✅ 연결 재시도 루프
        public async Task StartAutoConnectAsync(int retryDelayMs = 3000)
        {
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;
            bool lastConnected = false;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_device == null || _device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                    {
                        bool ok = await ConnectAsync();
                        if (ok != lastConnected)
                        {
                            ConnectionChanged?.Invoke(ok);
                            lastConnected = ok;
                        }
                    }

                    await Task.Delay(retryDelayMs, token);
                }
            }, token);
        }

        public async Task<bool> ConnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {

                if (_device != null)
                {
                    _device.ConnectionStatusChanged -= OnConnectionStatusChanged; // 기존 이벤트 해제
                    _device.Dispose();
                    _device = null;
                    _commandCharacteristic = null;

                }


                ulong address = ConvertMacToULong(_macAddress);

                var selector = BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(address);
                var devices = await DeviceInformation.FindAllAsync(selector);
                if (devices.Count == 0)
                {
                    Debug.WriteLine("[BLE] ⚠ Device not found (power off?)");
                    return false;
                }

                _device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);
                if (_device == null)
                {
                    Debug.WriteLine("[BLE] ❌ Device creation failed.");
                    return false;
                }
                await Task.Delay(1000);
                _device.ConnectionStatusChanged += OnConnectionStatusChanged;

                var services = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                foreach (var service in services.Services)
                {
                    var characteristics = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        if (characteristic.Uuid == _cmdUuid)
                        {
                            Debug.WriteLine("[BLE] Success Connect");
                            _commandCharacteristic = characteristic;
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
            finally
            {
                _connectLock.Release();
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
                //byte[] bytes = ConvertHexToBytes(hexCommand);
                //using var writer = new DataWriter();
                //writer.WriteBytes(bytes);
                //var status = await _commandCharacteristic.WriteValueAsync(writer.DetachBuffer());
                //Debug.WriteLine($"[BLE] → Sent {hexCommand} (Status: {status})");

                byte[] bytes = ConvertHexToBytes(hexCommand);
                using var writer = new DataWriter();
                writer.WriteBytes(bytes);
                var status = await _commandCharacteristic.WriteValueAsync(writer.DetachBuffer());

                if (status != GattCommunicationStatus.Success)
                {
                    Debug.WriteLine($"[BLE] ❌ Write failed, status={status}");
                    _commandCharacteristic = null;
                    _ = ConnectAsync();
                }
                else
                {
                    Debug.WriteLine($"[BLE] → Sent {hexCommand} (Status: {status})");
                }
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

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == _lastStatus)
                return; // 🔹 상태 변화 없으면 무시

            _lastStatus = sender.ConnectionStatus;
            bool connected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
            Debug.WriteLine($"[BLE] 🔔 Connection changed → {connected}");
            ConnectionChanged?.Invoke(connected);
        }
        public void Dispose()
        {
            _reconnectCts?.Cancel();
            _commandCharacteristic = null;
            _device?.Dispose();
            _device = null;
        }
    }
}