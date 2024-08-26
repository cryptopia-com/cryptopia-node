using Cryptopia.Node.RTC.Messages.Payloads;
using Newtonsoft.Json;

namespace Cryptopia.Node.RTC.Messages
{
    /// <summary>
    /// Message types for the RTC protocol
    /// </summary>
    public enum RTCMessageType
    {
        Offer,
        Answer,
        Rejection,
        Candidate,
        Broadcast,
        Relay
    }

    /// <summary>
    /// Represents a message in the RTC protocol
    /// </summary>
    public abstract class RTCMessage
    {
        [JsonProperty("type")]
        public RTCMessageType Type { get; protected set; }
    }

    /// <summary>
    /// Represents an offer message in the RTC protocol
    /// </summary>
    public class RTCOfferMessage : RTCMessage
    {
        /// <summary>
        /// Offer SDP payload
        /// </summary>
        [JsonProperty("offer")]
        public required SDPInfo? Offer;

        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCOfferMessage()
        {
            Type = RTCMessageType.Offer;
        }
    }

    /// <summary>
    /// Represents an answer message in the RTC protocol
    /// </summary>
    public class RTCAnswerMessage : RTCMessage
    {
        /// <summary>
        /// Answer SDP payload
        /// </summary>
        [JsonProperty("answer")]
        public required SDPInfo Answer;

        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCAnswerMessage()
        {
            Type = RTCMessageType.Answer;
        }
    }

    /// <summary>
    /// Represents a rejection message in the RTC protocol
    /// </summary>
    public class RTCRejectionMessage : RTCMessage
    {
        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCRejectionMessage()
        {
            Type = RTCMessageType.Rejection;
        }
    }

    /// <summary>
    /// Represents an ICE candidate message in the RTC protocol
    /// </summary>
    public class RTCCandidateMessage : RTCMessage
    {
        /// <summary>
        /// ICE candidate
        /// </summary>
        [JsonProperty("candidate")]
        public required IceCandidate Canidate;

        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCCandidateMessage()
        {
            Type = RTCMessageType.Candidate;
        }
    }

    /// <summary>
    /// Represents message to be broadcasted by a node to connected peers
    /// 
    /// TODO: Add constraints for the audience
    /// </summary>
    public class RTCBroadcastMessage : RTCMessage
    {
        // TODO: Add constraints for the audience

        /// <summary>
        /// The text message to be broadcasted
        /// </summary>
        [JsonProperty("text")]
        public required string Text;

        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCBroadcastMessage()
        {
            Type = RTCMessageType.Broadcast;
        }
    }

    /// <summary>
    /// Represents a direct message to be sent to a specific peer
    /// </summary>
    public class RTCRelayMessage : RTCMessage
    {
        /// <summary>
        /// The receiver to relay the message to
        /// </summary>
        [JsonProperty("receiver")]
        public required RTCReceiver Receiver;

        /// <summary>
        /// The text message to be sent to the peer
        /// </summary>
        [JsonProperty("text")]
        public required string Text;

        /// <summary>
        /// Constructor setting the message type
        /// </summary>
        public RTCRelayMessage()
        {
            Type = RTCMessageType.Relay;
        }
    }
}
