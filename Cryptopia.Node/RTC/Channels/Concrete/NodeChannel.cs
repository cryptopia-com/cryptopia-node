using Cryptopia.Node.RTC.Channels.Types;
using Cryptopia.Node.RTC.Extensions;
using Cryptopia.Node.RTC.Messages;
using Cryptopia.Node.RTC.Messages.Payloads;
using Cryptopia.Node.RTC.Signalling;
using Cryptopia.Node.Services.Logging;

namespace Cryptopia.Node.RTC.Channels.Concrete
{
    /// <summary>
    /// A Channel from a node (us) to another node (them)
    /// </summary>
    public class NodeChannel : BaseChannel<NodeChannel>, INodeChannel
    {
        /// <summary>
        /// The node account (us)
        /// </summary>
        public LocalAccount OriginSigner { get; private set; }

        /// <summary>
        /// The signer of the destination account (them)
        /// </summary>
        public ExternalAccount DestinationSigner { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="isPolite"></param>
        /// <param name="isInitiatedByUs"></param>
        /// <param name="loggingService"></param>
        /// <param name="signallingService"></param>
        /// <param name="originSigner"></param>
        /// <param name="destinationSigner"></param>
        public NodeChannel(
            bool isPolite,
            bool isInitiatedByUs,
            ILoggingService? loggingService,
            ISignallingService signallingService,
            LocalAccount originSigner,
            ExternalAccount destinationSigner)
            : base(isPolite, isInitiatedByUs, loggingService, signallingService)
        {
            OriginSigner = originSigner;
            DestinationSigner = destinationSigner;
        }

        /// <summary>
        /// Gather channel data for logging
        /// </summary>
        /// <returns></returns>
        protected override IDictionary<string, string> GatherChannelData()
        {
            return new Dictionary<string, string>
            {
                { "type", "Node Channel" },
                { "origin", OriginSigner.Address },
                { "destination", DestinationSigner.Address }
            };
        }

        /// <summary>
        /// Sends an SDP offer
        /// 
        /// Transmits an SDP offer to the remote peer to initiate the WebRTC handshake
        /// </summary>
        /// <param name="offer">The SDP offer to send</param>
        /// <returns></returns>
        protected override void SendOffer(SDPInfo offer)
        {
            SignallingService.Send(new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = "Node",
                    Signer = DestinationSigner.Address
                },
                Sender = new RTCSender()
                {
                    Account = "Node",
                    Signer = OriginSigner.Address
                },
                Payload = new RTCOfferMessage()
                {
                    Offer = offer
                },
                Signature = ""
            });
        }

        /// <summary>
        /// Sends an SDP answer
        /// 
        /// Transmits an SDP answer to the remote peer to complete the WebRTC handshake
        /// </summary>
        /// <param name="answer">The SDP answer to send</param>
        /// <returns></returns>
        protected override void SendAnswer(SDPInfo answer)
        {
            var enveloppe = new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = "Node",
                    Signer = DestinationSigner.Address
                },
                Sender = new RTCSender()
                {
                    Account = "Node",
                    Signer = OriginSigner.Address
                },
                Payload = new RTCAnswerMessage()
                {
                    Answer = answer
                },
                Signature = ""
            };

            var channelData = GatherChannelData();
            channelData.Add("SignallingService.IsOpen", SignallingService.IsOpen.ToString());
            channelData.Add("SDPInfo.Type", answer.Type);
            channelData.Add("SDPInfo.SDP", answer.SDP);
            channelData.Add("RTCMessageEnvelope", enveloppe.Serialize());
            LoggingService?.LogInfo("Sending SDP answer", channelData);

            SignallingService.Send(enveloppe);
        }

        /// <summary>
        /// Sends an SDP rejection
        /// 
        /// Transmits an SDP rejection to the remote peer, indicating that the offer was not accepted
        /// </summary>
        /// <param name="offer">The SDP offer to reject</param>
        /// <returns></returns>
        protected override void SendRejection(SDPInfo offer)
        {
            SignallingService.Send(new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = "Node",
                    Signer = DestinationSigner.Address
                },
                Sender = new RTCSender()
                {
                    Account = "Node",
                    Signer = OriginSigner.Address
                },
                Payload = new RTCRejectionMessage(),
                Signature = ""
            });
        }

        /// <summary>
        /// Sends an ICE candidate
        /// 
        /// Transmits an ICE candidate to the remote peer to assist in establishing the connection
        /// </summary>
        /// <param name="candidate">The ICE candidate to send</param>
        /// <returns></returns>
        protected override void SendCandidate(IceCandidate candidate)
        {
            var envelope = new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = "Node",
                    Signer = DestinationSigner.Address
                },
                Sender = new RTCSender()
                {
                    Account = "Node",
                    Signer = OriginSigner.Address
                },
                Payload = new RTCCandidateMessage()
                {
                    Canidate = candidate,

                },
                Signature = ""
            };

            var channelData = GatherChannelData();
            channelData.Add("SignallingService.IsOpen", SignallingService.IsOpen.ToString());
            channelData.Add("candidate.Candidate", candidate.Candidate);
            channelData.Add("candidate.SdpMid", candidate.SdpMid);
            channelData.Add("RTCMessageEnvelope", envelope.Serialize());
            LoggingService?.LogInfo("Sending SDP candidate", channelData);

            SignallingService.Send(envelope);
        }

        /// <summary>
        /// Handles reception of an SDP rejection
        /// 
        /// Processes the received SDP rejection and updates the channel state accordingly
        /// </summary>
        protected override void OnReceiveRejection()
        {
            State = ChannelState.Rejected;
        }

        /// <summary>
        /// Handles reception of an ICE candidate
        /// 
        /// Processes the received ICE candidate and adds it to the local peer connection
        /// </summary>
        /// <param name="candidate">The ICE candidate received</param>
        protected override void OnReceiveCandidate(IceCandidate candidate)
        {
            AddIceCandidate(candidate);
        }

        /// <summary>
        /// Called when a message is received
        /// 
        /// Handles incoming messages encapsulated in an RTCMessageEnvelope
        /// </summary>
        /// <param name="envelope">The received RTC message envelope</param>
        protected override void OnReceiveMessage(RTCMessageEnvelope envelope)
        {
            // Nothing to do here
        }

        /// <summary>
        /// Called when the heartbeat times out
        /// </summary>
        protected override void OnHeartbeatTimeoutDetected()
        {
            base.OnHeartbeatTimeoutDetected();

            // Log the timeout
            LoggingService?.LogError(
                "Heartbeat timeout", GatherChannelData());
        }

        /// <summary>
        /// Called when high latency is detected
        /// </summary>
        /// <param name="latency"></param>
        protected override void OnHighLatencyDetected(double latency)
        {
            base.OnHighLatencyDetected(latency);

            // Log the high latency
            var data = GatherChannelData();
            data.Add("latency", latency.ToString());
            LoggingService?.LogWarning("High latency detected", data);
        }
    }
}
