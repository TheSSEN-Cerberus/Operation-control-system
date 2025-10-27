using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class LaserControlData
    {
        [JsonPropertyName("state")]
        public bool isOn { get; set; }
    }
}
