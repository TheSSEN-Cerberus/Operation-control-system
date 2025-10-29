using System.Text.Json.Serialization;


namespace Operation_Control_System.Models
{
    public class EmergencyStopData
    {
        [JsonPropertyName("reset")]
        public bool reset { get; set; }
    }
}