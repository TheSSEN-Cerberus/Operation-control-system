using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class TrackTargetData : MessageData
    {
        [JsonPropertyName("target_id")]
        public int TargetId { get; set; }
    }
}
