using Newtonsoft.Json;

namespace Cryptopia.Node.RTC.Messages
{
    /// <summary>
    /// RTC message envelope
    /// 
    /// Contains metadata, sender, receiver, payload and signature
    /// </summary>
    public class RTCMessageEnvelope
    {
        /// <summary>
        /// Timestamp at which the message was created
        /// </summary>
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Maximum age of the message in seconds before it is discarded
        /// </summary>
        [JsonProperty("maxAge")]
        public int MaxAge { get; set; }

        /// <summary>
        /// Priority of the message
        /// </summary>
        [JsonProperty("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Sequence number of the message in the channel
        /// </summary>
        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        /// <summary>
        /// The sender of the message and signer of the signature
        /// </summary>
        [JsonProperty("sender")]
        public required RTCSender Sender;

        /// <summary>
        /// The receiver of the message
        /// </summary>
        [JsonProperty("receiver")]
        public required RTCReceiver Receiver;

        /// <summary>
        /// The actual message payload
        /// </summary>
        [JsonProperty("payload")]
        public required RTCMessage Payload { get; set; }

        /// <summary>
        /// The signature that proves the sender of the message
        /// </summary>
        [JsonProperty("signature")]
        public string? Signature { get; set; }

        public bool IsExpired()
        {
            return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds() - Timestamp > MaxAge;
        }

        /// <summary>
        /// Signs the RTCMessageEnvelope with a private key
        /// </summary>
        /// <returns></returns>
        public RTCMessageEnvelope Sign(string privateKey)
        {
            // TODO: Implement signing
            return this;
        }

        /// <summary>
        /// Verifies the signature of a RTCMessageEnvelope
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        public bool Verify()
        {
            // TODO: Implement verification
            return true;
        }
    }
}
