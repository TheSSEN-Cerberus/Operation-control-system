using System.Text.Json.Serialization;

namespace Operation_Control_System.Models
{
    public class StatusData : MessageData
    {
        [JsonPropertyName("network_rssi")]
        public int NetworkRssi { get; set; }

        [JsonPropertyName("packet_loss")]
        public float PacketLoss { get; set; }

        [JsonPropertyName("health")]
        public string Health { get; set; } = "OK";

        [JsonPropertyName("pos_yaw")]
        public float PosYaw { get; set; }

        [JsonPropertyName("pos_pitch")]
        public float PosPitch { get; set; }

        [JsonPropertyName("accel")]
        public float[] Accel { get; set; } = new float[3];

        [JsonPropertyName("gyro")]
        public float[] Gyro { get; set; } = new float[3];

        [JsonPropertyName("mag")]
        public float[] Mag { get; set; } = new float[3];
    }
}
