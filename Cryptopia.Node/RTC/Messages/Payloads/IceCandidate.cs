using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public class IceCandidate
    {
        [JsonProperty("candidate")]
        public required string Candidate { get; set; }

        [JsonProperty("sdpmid")]
        public required string SdpMid { get; set; }

        [JsonProperty("sdpmlineindex")]
        public int? SdpMLineIndex { get; set; }
    }
}
