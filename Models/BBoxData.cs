using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class BBoxData
    {

        [JsonPropertyName("frame_id")]
        public int FrameId { get; set; }

        [JsonPropertyName("objects")]
        public List<DetectedObject> Objects { get; set; } = new();
    }
}
