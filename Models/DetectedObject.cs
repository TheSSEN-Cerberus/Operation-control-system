using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class DetectedObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("class")]
        public string Class { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("bbox")]
        public int[] BBox { get; set; } = new int[4]; // x1,y1,x2,y2
    }
}
