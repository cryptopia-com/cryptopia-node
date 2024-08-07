﻿using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
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
        public required string Signature { get; set; }
    }
}
