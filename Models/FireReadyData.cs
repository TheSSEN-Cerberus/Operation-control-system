using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireReadyData 
    {
        [JsonPropertyName("target_id")]
        public int TargetId { get; set; }

        [JsonPropertyName("ready")]
        public bool IsReady { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }
    }
}