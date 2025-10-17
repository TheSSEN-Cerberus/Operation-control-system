using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireCommandData : MessageData
    {
        [JsonPropertyName("trigger")]
        public bool Trigger { get; set; }
    }
}
