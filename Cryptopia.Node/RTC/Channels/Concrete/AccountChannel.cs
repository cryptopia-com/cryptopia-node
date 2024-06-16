namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// A Channel from a node (us) to an account that is registered with 
    /// </summary>
    public class AccountChannel : BaseChannel, IAccountChannel
    {
        /// <summary>
        /// The label for the channel
        /// </summary>
        public static string CHANNEL_LABEL = "ACCOUNT_DATA_CHANNEL";

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

        public override string GetLabel()
        { 
            return CHANNEL_LABEL;
        }

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

        protected override void SendRejection()
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

        protected override void OnReceiveRejection()
        {
            State = ChannelState.Rejected;
        }

        protected override void OnReceiveCandidate(IceCandidate candidate)
        {
            AddIceCandidate(candidate);
        }
    }
}
