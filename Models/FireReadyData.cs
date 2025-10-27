using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireReadyData 
    {
        [JsonPropertyName("IsReady")]
        public bool IsReady { get; set; }
    }
}