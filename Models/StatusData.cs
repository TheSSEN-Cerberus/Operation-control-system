using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class StatusData 
    {
        [JsonPropertyName("yaw")]
        public double Yaw { get; set; }

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; }
    }
}
