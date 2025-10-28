using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// 날짜별 자동 분리형 CSV 로거
    /// </summary>
    public class LoggerService
    {
        private readonly string _logDir;
        private readonly object _lock = new();
        private string _currentFilePath = "";
        private string _currentDate = "";

        public LoggerService()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);

            UpdateLogFilePath();
        }

        /// <summary>
        /// TX/RX 메시지 기록
        /// </summary>
        public void Log(string direction, string type, string ip, int port, string payload)
        {
            try
            {
                lock (_lock)
                {
                    UpdateLogFilePath();

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    payload = payload.Replace("\n", "").Replace("\r", "").Replace(",", ";");

                    string line = $"{timestamp},{direction},{type},{ip},{port},{payload}\n";
                    File.AppendAllText(_currentFilePath, line, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logger] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 날짜가 바뀌면 자동으로 새로운 로그 파일 생성
        /// </summary>
        private void UpdateLogFilePath()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today == _currentDate && File.Exists(_currentFilePath))
                return;

            _currentDate = today;
            _currentFilePath = Path.Combine(_logDir, $"{_currentDate}.csv");

            if (!File.Exists(_currentFilePath))
            {
                File.AppendAllText(_currentFilePath, "Timestamp,Direction,Type,IP,Port,Payload\n", Encoding.UTF8);
                System.Diagnostics.Debug.WriteLine($"[Logger] Created new log file: {_currentFilePath}");
            }
        }
    }
}