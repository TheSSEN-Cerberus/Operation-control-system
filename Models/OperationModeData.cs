using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class TrackModeData
    {
        [JsonPropertyName("mode")]
        public int Mode { get; set; } // 0: 수동, 1: 반자동, 2: 자동
    }
}