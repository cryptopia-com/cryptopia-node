using Cryptopia.Node.RTC;
using WebSocketSharp.Server;
using WebSocketSharp;

public class RTCSignallingBehavior : WebSocketBehavior, ISignallingService
{
    public bool IsOpen => State == WebSocketState.Open;

    public new event EventHandler? OnOpen;
    public event EventHandler<RTCMessageEnvelope>? OnReceiveMessage;

    public void Connect()
    {
        
    }

    public void Disconnect()
    {
        
    }

    public void Send(RTCMessageEnvelope message)
    {
        Send(message.Serialize());
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (e.IsText && e.Data.IsRTCMessage())
        {
            var message = e.Data.DeserializeRTCMessage();
            if (null == message || null == message.Payload)
            {
                throw new ArgumentException("Missing payload");
            }

            if (message.Payload.Type == RTCMessageType.Offer)
            {
                HandleOfferMessage(message);
            }
            else
            { 
                OnReceiveMessage?.Invoke(this, message);
            }
        }
    }

    private async void HandleOfferMessage(RTCMessageEnvelope message)
    {
        //Console.WriteLine($"Received offer from {message.Sender.Account}");

        var payload = (RTCOfferMessage)message.Payload;
        var offer = payload.Offer;

        if (null == offer)
        {
            Console.WriteLine("(!) Offer cannot be empty");
            return;
        }

        await ChannelManager.Instance
            .CreateChannel(
                message.Sender.Account, 
                message.Sender.Signer, 
                this)
            .AcceptAsync(offer);
    }
}