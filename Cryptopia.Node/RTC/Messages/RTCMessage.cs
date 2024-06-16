using Newtonsoft.Json;

namespace Cryptopia.Node.RTC
{
    public enum RTCMessageType
    {
        Offer,
        Answer,
        Rejection,
        Candidate,
        Text
    }

    public abstract class RTCMessage
    {
        [JsonProperty("type")]
        public RTCMessageType Type { get; protected set; }
    }

    public class RTCOfferMessage : RTCMessage
    {
        [JsonProperty("offer")]
        public required SDPInfo? Offer;

        public RTCOfferMessage()
        {
            Type = RTCMessageType.Offer;
        }
    }

    public class RTCAnswerMessage : RTCMessage
    {
        [JsonProperty("answer")]
        public required SDPInfo Answer;

        public RTCAnswerMessage()
        {
            Type = RTCMessageType.Answer;
        }
    }

    public class RTCRejectionMessage : RTCMessage
    {
        public RTCRejectionMessage()
        {
            Type = RTCMessageType.Rejection;
        }
    }

    public class RTCCandidateMessage : RTCMessage
    {
        [JsonProperty("candidate")]
        public required IceCandidate Canidate;

        public RTCCandidateMessage()
        {
            Type = RTCMessageType.Candidate;
        }
    }

    public class RTCTextMessage : RTCMessage
    {
        [JsonProperty("text")]
        public required string Text;

        public RTCTextMessage()
        {
            Type = RTCMessageType.Text;
        }
    }
}
