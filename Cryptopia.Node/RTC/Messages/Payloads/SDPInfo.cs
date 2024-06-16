using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public class SDPInfo
    {
        [JsonProperty("type")]
        public required string Type;

        [JsonProperty("sdp")]
        public required string SDP;
    }
}
