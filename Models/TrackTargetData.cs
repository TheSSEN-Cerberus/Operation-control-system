using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class TrackTargetData
    {
        [JsonPropertyName("target_id")]
        public int TargetId { get; set; }
        public TrackTargetData(int id)
        {
            TargetId = id;
        }

    }   
}
