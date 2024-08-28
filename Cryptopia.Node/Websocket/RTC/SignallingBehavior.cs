using WebSocketSharp.Server;
using WebSocketSharp;
using Cryptopia.Node.RTC;
using Cryptopia.Node.RTC.Extensions;
using Cryptopia.Node.RTC.Messages;
using Cryptopia.Node.RTC.Signalling;
using Cryptopia.Node.Services.Logging;

namespace Cryptopia.Node.RPC
{
    /// <summary>
    /// RTCSignallingBehavior - Responsible for handling WebSocket connections and RTC signaling messages.
    /// 
    /// This class manages the WebSocket connection, processes incoming RTC messages, 
    /// and sends RTC messages through the WebSocket connection.
    /// </summary>
    public class SignallingBehavior : WebSocketBehavior, ISignallingService
    {
        /// <summary>
        /// Indicates whether the WebSocket connection is open
        /// </summary>
        public bool IsOpen => State == WebSocketState.Open;

        // Internal
        public ILoggingService? LoggingService { get; private set;}

        // Events
        public new event EventHandler? OnOpen;
        public event EventHandler<RTCMessageEnvelope>? OnReceiveMessage;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggingService"></param>
        public SignallingBehavior(ILoggingService? loggingService = null)
        {
            LoggingService = loggingService;
        }

        /// <summary>
        /// Connects the signaling service 
        /// </summary>
        public void Connect()
        {
            // Implementation is empty as it relies on WebSocketBehavion
        }

        /// <summary>
        /// Disconnects the signaling service
        /// </summary>
        public void Disconnect()
        {
            // Implementation is empty as it relies on WebSocketBehavior
        }

        /// <summary>
        /// Sends an RTC message envelope through the WebSocket
        /// </summary>
        /// <param name="message">The RTC message envelope to send</param>
        public void Send(RTCMessageEnvelope message)
        {
            Send(message.Serialize());
        }

        /// <summary>
        /// Handles incoming WebSocket messages
        /// </summary>
        /// <param name="e">The message event arguments containing the received message</param>
        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText && e.Data.IsRTCMessage())
            {
                var message = e.Data.DeserializeRTCMessage();
                if (null == message || null == message.Payload)
                {
                    var exception = new ArgumentException("Missing payload");
                    LoggingService?.LogException(exception, new Dictionary<string, string>
                    {
                        { "data", e.Data }
                    });

                    throw exception;
                }

                // Check Signature (check message signed by sender)

                // Check MaxAge (check if message is not expired)

                // Check Sender (check if sender is valid account or node onchain)

                if (message.Payload.Type == RTCMessageType.Offer)
                {
                    _ = Task.Run(async () =>
                    {
                        await HandleOfferMessageAsync(message);
                    });
                }

                else
                { 
                    OnReceiveMessage?.Invoke(this, message);
                }
            }
        }

        /// <summary>
        /// Handles incoming offer messages asynchronously
        /// </summary>
        private async Task HandleOfferMessageAsync(RTCMessageEnvelope message)
        {
            var payload = (RTCOfferMessage)message.Payload;
            var offer = payload.Offer;

            if (null == offer)
            {
                LoggingService?.LogError(
                    "Offer cannot be empty", 
                    new Dictionary<string, string>
                    {
                       { "sender", message.Sender.ToString() },
                       { "receiver", message.Receiver.ToString() }
                    });
                return;
            }

            if (message.Sender.Account == null || message.Sender.Signer == null)
            {
                LoggingService?.LogError(
                    "Sender account or signer cannot be empty", 
                    new Dictionary<string, string>
                    {
                       { "sender", message.Sender.ToString() },
                       { "receiver", message.Receiver.ToString() }
                    });
                return;
            }

            // Check receiver (us)
            if (!AccountManager.Instance.IsSigner(message.Receiver.Signer))
            {
                LoggingService?.LogError(
                    "Receiver account not registered as node signer",
                    new Dictionary<string, string>
                    {
                        { "sender", message.Sender.ToString() },
                        { "receiver", message.Receiver.ToString() }
                    });
                return;
            }

            // Check sender (node|account)
            if (message.Sender.IsNode)
            {
                // Check if node is valid
                if (!(await IsValidNode(message.Sender)))
                {
                    LoggingService?.LogError(
                        "Invalid node",
                        new Dictionary<string, string>
                        {
                            { "sender", message.Sender.ToString() },
                            { "receiver", message.Receiver.ToString() }
                        });
                    return;
                }

                // Create channel
                await ChannelManager.Instance
                    .CreateNodeChannel(
                        message.Sender.Signer,
                        this)
                    .AcceptAsync(offer);
            }
            else
            {
                // Check if account is valid
                if (!(await IsValiAccount(message.Sender)))
                {
                    LoggingService?.LogError(
                        "Invalid account",
                        new Dictionary<string, string>
                        {
                            { "sender", message.Sender.ToString() },
                            { "receiver", message.Receiver.ToString() }
                        });
                    return;
                }

                // Create channel
                await ChannelManager.Instance
                    .CreateAccountChannel(
                        message.Sender.Account,
                        message.Sender.Signer,
                        this)
                    .AcceptAsync(offer);
            }
        }

        private Task<bool> IsValidNode(RTCSender sender)
        {
            return Task.FromResult(true);
        }

        private Task<bool> IsValiAccount(RTCSender sender)
        {
            return Task.FromResult(true);
        }
    }
}