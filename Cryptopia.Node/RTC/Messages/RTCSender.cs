using Newtonsoft.Json;

namespace Cryptopia.Node.RTC.Messages
{
    public class RTCSender
    {
        [JsonProperty("account")]
        public required string Account;

        [JsonProperty("signer")]
        public required string Signer;

        /// <summary>
        /// True if the sender is a node.
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
