namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// A Channel from a node (us) to an account that is registered with 
    /// </summary>
    public class AccountChannel : BaseChannel, IAccountChannel
    {
        /// <summary>
        /// The node account (us)
        /// </summary>
        public LocalAccount OriginSigner { get; private set; }

        /// <summary>
        /// The signer of the destination account (local)
        /// </summary>
        public LocalAccount DestinationSigner { get; private set; }

        /// <summary>
        /// The registered destination account (smart-contract)
        /// </summary>
        public RegisteredAccount DestinationAccount { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="loggingService"></param>
        /// <param name="signallingService"></param>
        /// <param name="originSigner"></param>
        /// <param name="destinationSigner"></param>
        /// <param name="destinationAccount"></param>
        public AccountChannel(
            IChannelConfig config, 
            ILoggingService loggingService, 
            ISignallingService signallingService, 
            LocalAccount originSigner, 
            LocalAccount destinationSigner, 
            RegisteredAccount destinationAccount) 
            : base(config, loggingService, signallingService)
        {
            OriginSigner = originSigner;
            DestinationSigner = destinationSigner;
            DestinationAccount = destinationAccount;
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
            SignallingService.Send(new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = DestinationAccount.Address,
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
            });
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
                    Account = DestinationAccount.Address,
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
            if (null == SignallingService)
            {
                throw new ArgumentNullException("Signalling is null");
            }

            if (null == candidate)
            {
                throw new ArgumentNullException("Candidate is null");
            }

            if (null == DestinationAccount)
            {
                throw new ArgumentNullException("Destination account is null");
            }

            if (null == DestinationSigner)
            {
                throw new ArgumentNullException("Destination signer is null");
            }

            if (null == OriginSigner)
            {
                throw new ArgumentNullException("Origin signer is null");
            }

            SignallingService.Send(new RTCMessageEnvelope()
            {
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                MaxAge = 60, // 1 minute
                Priority = 1,
                Sequence = 0,
                Receiver = new RTCReceiver()
                {
                    Account = DestinationAccount.Address,
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
            });
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
    }
}
