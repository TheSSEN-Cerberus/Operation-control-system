using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireReadyData 
    {
        [JsonPropertyName("ready")]
        public bool IsReady { get; set; }
    }
}