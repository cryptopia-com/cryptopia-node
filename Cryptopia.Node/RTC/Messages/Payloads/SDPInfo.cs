using Newtonsoft.Json;

namespace Cryptopia.Node.RTC.Messages.Payloads
{
    public class SDPInfo
    {
        [JsonProperty("type")]
        public required string Type;

        [JsonProperty("sdp")]
        public required string SDP;
    }
}
