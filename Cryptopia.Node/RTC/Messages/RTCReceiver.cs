using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public class RTCReceiver
    {
        [JsonProperty("account")]
        public required string Account;

        [JsonProperty("signer")]
        public required string Signer;
    }
}
