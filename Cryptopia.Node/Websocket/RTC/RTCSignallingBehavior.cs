using Cryptopia.Node.RTC;
using WebSocketSharp.Server;
using WebSocketSharp;
using Cryptopia.Node;

/// <summary>
/// RTCSignallingBehavior - Responsible for handling WebSocket connections and RTC signaling messages.
/// 
/// This class manages the WebSocket connection, processes incoming RTC messages, 
/// and sends RTC messages through the WebSocket connection.
/// </summary>
public class RTCSignallingBehavior : WebSocketBehavior, ISignallingService
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
    public RTCSignallingBehavior(ILoggingService? loggingService = null)
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

            if (message.Payload.Type == RTCMessageType.Offer)
            {
                Task.Run(() => HandleOfferMessageAsync(message));
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
    private async void HandleOfferMessageAsync(RTCMessageEnvelope message)
    {
        var payload = (RTCOfferMessage)message.Payload;
        var offer = payload.Offer;

        if (null == offer)
        {
            LoggingService?.LogError(
                "Offer cannot be empty", 
                new Dictionary<string, string>
                {
                    { "node", message.Receiver.Account },
                    { "account", message.Sender.Account },
                    { "signer", message.Sender.Signer }
                });
            return;
        }

        if (message.Sender.Account == null || message.Sender.Signer == null)
        {
            LoggingService?.LogError(
                "Sender account or signer cannot be empty", 
                new Dictionary<string, string>
                {
                    { "node", message.Receiver.Account }
                });
            return;
        }

        // Verify signature
        if (!message.Verify())
        {
            LoggingService?.LogError(
                "Offer verification failed", 
                new Dictionary<string, string>
                {
                    { "node", message.Receiver.Account },
                    { "account", message.Sender.Account },
                    { "signer", message.Sender.Signer }
                });
            return;
        }

        // Verify age
        if (message.IsExpired())
        {
            LoggingService?.LogWarning(
                "Offer expired", 
                new Dictionary<string, string>
                {
                    { "node", message.Receiver.Account },
                    { "account", message.Sender.Account },
                    { "signer", message.Sender.Signer }
                });
            return;
        }

        var account = message.Sender.Account;
        var signer = message.Sender.Signer;

        // Remove existing channel (if any)
        if (ChannelManager.Instance.IsKnown(account, signer))
        { 
            LoggingService?.LogWarning(
                "Removing existing channel", 
                new Dictionary<string, string>
                {
                    { "reason", "Received new offer" },
                    { "node", message.Receiver.Account },
                    { "account", account },
                    { "signer", signer }
                });

            ChannelManager.Instance.RemoveChannel(
                account, signer);
        }

        try
        {
            // Create a new channel
            await ChannelManager.Instance
                .CreateChannel(
                    message.Sender.Account,
                    message.Sender.Signer,
                    this)
                .AcceptAsync(offer);
        }
        catch (Exception ex)
        {
            LoggingService?.LogException(ex, new Dictionary<string, string>
            {
                { "event", "HandleOfferMessageAsync" },
                { "node", message.Receiver.Account },
                { "account", account },
                { "signer", signer }
            });
        }
    }
}