using Newtonsoft.Json;

namespace Cryptopia.Node.RTC.Messages
{
    public class RTCReceiver
    {
        [JsonProperty("account")]
        public required string Account;

        [JsonProperty("signer")]
        public required string Signer;

        /// <summary>
        /// True if the receiver is a node.
        /// </summary>
        public bool IsNode
        {
            get
            {
                return Account == "node";
            }
        }
    }
}
