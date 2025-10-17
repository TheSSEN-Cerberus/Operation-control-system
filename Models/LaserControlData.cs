using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class LaserControlData : MessageData
    {
        [JsonPropertyName("laser_on_off")]
        public bool isOn { get; set; }
    }
}
