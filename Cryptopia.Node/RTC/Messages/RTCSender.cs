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
                return Account.Equals("node", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Represents the sender as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Account} ({Signer})";
        }
    }
}
