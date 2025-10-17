using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class GimbalControlData : MessageData
    {
        [JsonPropertyName("azimuth")]
        public float Azimuth { get; set; }

        [JsonPropertyName("elevation")]
        public float Elevation { get; set; }
    }
}
