using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class GimbalControlData
    {
        [JsonPropertyName("yaw")]
        public float Yaw { get; set; }

        [JsonPropertyName("pitch")]
        public float Pitch { get; set; }
    }
}
