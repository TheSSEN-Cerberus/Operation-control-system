using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireReadyData : MessageData
    {
        [JsonPropertyName("IsReady")]
        public bool IsReady { get; set; }
    }
}