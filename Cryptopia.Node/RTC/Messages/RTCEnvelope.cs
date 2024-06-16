using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public class RTCMessageEnvelope
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("maxAge")]
        public int MaxAge { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        [JsonProperty("sender")]
        public required RTCSender Sender;

        [JsonProperty("receiver")]
        public required RTCReceiver Receiver;

        [JsonProperty("payload")]
        public required RTCMessage Payload { get; set; }

        [JsonProperty("signature")]
        public required string Signature { get; set; }
    }
}
