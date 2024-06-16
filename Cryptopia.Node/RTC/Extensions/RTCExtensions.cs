using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WebSocketSharp;

namespace Cryptopia.Node.RTC
{
    public static class RTCExtensions
    {
        /// <summary>
        /// Serialize a RTCMessageEnvelope to JSON string, including type information
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(this RTCMessageEnvelope obj)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.None
            };

            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// Returns true if json is a RTC Message
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static bool IsRTCMessage(this string json)
        {
            if (json.IsNullOrEmpty())
            {
                return false;
            }

            var envelopeToken = JObject.Parse(json);

            var payloadToken = envelopeToken["payload"];
            if (payloadToken == null)
            {
                return false;
            }

            var payloadTokenType = (string)payloadToken["type"];
            if (payloadTokenType == null)
            {
                return false;
            }

            // Check the type of the message
            if (!Enum.TryParse(typeof(RTCMessageType), payloadTokenType, out object? result))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Custom deserialization for RTCMessageEnvelope that handles polymorphic RTCMessage types
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static RTCMessageEnvelope DeserializeRTCMessage(this string json)
        {
            var envelopeToken = JObject.Parse(json);
            var payloadToken = envelopeToken["payload"];

            if (payloadToken == null) 
            {
                throw new InvalidCastException("Missing payload");
            }

            // Check the type of the message and deserialize accordingly
            var messageType = (RTCMessageType)Enum.Parse(
                typeof(RTCMessageType), (string)payloadToken["type"]);

            var payload = default(RTCMessage);
            switch (messageType)
            {
                case RTCMessageType.Offer:
                    payload = payloadToken.ToObject<RTCOfferMessage>();
                    break;
                case RTCMessageType.Answer:
                    payload = payloadToken.ToObject<RTCAnswerMessage>();
                    break;
                case RTCMessageType.Rejection:
                    payload = payloadToken.ToObject<RTCRejectionMessage>();
                    break;
                case RTCMessageType.Candidate:
                    payload = payloadToken.ToObject<RTCCandidateMessage>();
                    break;
                case RTCMessageType.Text:
                    payload = payloadToken.ToObject<RTCTextMessage>();
                    break;   
            }

            if (null == payload)
            {
                throw new InvalidOperationException("Unsupported message type");
            }

            return new RTCMessageEnvelope
            {
                Timestamp = (long)envelopeToken["timestamp"],
                MaxAge = (int)envelopeToken["maxAge"],
                Priority = (int)envelopeToken["priority"],
                Sequence = (long)envelopeToken["sequence"],
                Signature = (string)envelopeToken["signature"],
                Sender = envelopeToken["sender"].ToObject<RTCSender>(),
                Receiver = envelopeToken["receiver"].ToObject<RTCReceiver>(),
                Payload = payload
            };
        }
    }
}
