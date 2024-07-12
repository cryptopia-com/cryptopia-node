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
        /// </summary>
        /// <param name="answer"></param>
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
        /// </summary>
        /// <param name="offer"></param>
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
        /// </summary>
        /// <param name="candidate"></param>
        protected override void SendCandidate(IceCandidate candidate)
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
                Payload = new RTCCandidateMessage()
                {
                    Canidate = candidate,

                },
                Signature = ""
            });
        }

        /// <summary>
        /// Called when an rejection is received
        /// </summary>
        protected override void OnReceiveRejection()
        {
            State = ChannelState.Rejected;
        }

        /// <summary>
        /// Called when an ICE candidate is received
        /// </summary>
        /// <param name="candidate"></param>
        protected override void OnReceiveCandidate(IceCandidate candidate)
        {
            AddIceCandidate(candidate);
        }

        /// <summary>
        /// Called when a message is received
        /// </summary>
        /// <param name="envelope"></param>
        protected override void OnReceiveMessage(RTCMessageEnvelope envelope)
        { 
           // Nothing to do here
        }
    }
}
