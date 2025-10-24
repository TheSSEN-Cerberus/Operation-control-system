using System;
using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    /// <summary>
    /// CSU 기반 메시지 최상위 클래스
    /// </summary>
    public class Message
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;  // 예: "Status", "FireReady", "FireCommand" 등

        [JsonPropertyName("timestamp")]
        public string TimeStamp { get; set; } = DateTime.UtcNow.ToString("O");

        [JsonPropertyName("data")]
        public MessageData? Data { get; set; }
    }
}
