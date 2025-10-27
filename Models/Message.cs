using System;
using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class Message<T>
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string TimeStamp { get; set; } = DateTime.UtcNow.ToString("O");

        [JsonPropertyName("data")]
        public T Data { get; set; }
    }

}
