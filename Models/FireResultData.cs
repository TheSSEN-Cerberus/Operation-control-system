using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class FireResultData : MessageData
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } // True: 명중, False: 빗나감
    }
}
