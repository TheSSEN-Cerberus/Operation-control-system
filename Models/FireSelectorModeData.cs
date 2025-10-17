using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireSelectorModeData : MessageData
    {
        [JsonPropertyName("mode")]
        public int Mode { get; set; } // 0: 수동, 1: 자동
    }
}
