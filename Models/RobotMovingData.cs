using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class RobotMovingData
    {
        [JsonPropertyName("moving")]
        public int Moving { get; set; } // 0: 정지, 1: 전진
    }
}
