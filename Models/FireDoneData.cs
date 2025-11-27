using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireDoneData
    {
        [JsonPropertyName("target_id")]
        public int TargetId { get; set; }
    }
}
