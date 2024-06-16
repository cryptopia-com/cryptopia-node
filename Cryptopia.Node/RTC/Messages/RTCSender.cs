using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public class RTCSender
    {
        [JsonProperty("account")]
        public required string Account;

        [JsonProperty("signer")]
        public required string Signer;
    }
}
