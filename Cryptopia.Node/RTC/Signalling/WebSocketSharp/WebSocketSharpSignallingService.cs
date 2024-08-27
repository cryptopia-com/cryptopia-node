using Cryptopia.Node.RTC.Extensions;
using Cryptopia.Node.RTC.Messages;
using Cryptopia.Node.Services.Logging;
using WebSocketSharp;

namespace Cryptopia.Node.RTC.Signalling.WebSocketSharp
{
    /// <summary>
    /// Service for RTC signalling with nodes using websockets
    /// </summary>
    public class WebSocketSharpSignallingService : ISignallingService, IDisposable
    {
        /// <summary>
        /// True if the connection is open
        /// </summary>
        public bool IsOpen { get; private set; }

        // Internal
        private WebSocket _Connection;

        // Events
        public event EventHandler? OnOpen;
        public event EventHandler<RTCMessageEnvelope>? OnReceiveMessage;

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="loggingService"></param>
        public WebSocketSharpSignallingService(string endpoint, ILoggingService loggingService)
        {
            _Connection = new WebSocket(endpoint);
            _Connection.OnOpen += (sender, e) =>
            {
                IsOpen = true;
                OnOpen?.Invoke(this, e);
            };

            _Connection.OnMessage += (sender, e) =>
            {
                OnReceiveMessage?.Invoke(
                    this, e.Data.DeserializeRTCMessage());
            };

            _Connection.OnError += (sender, e) =>
            {
                loggingService.LogError(e.Message);
            };
        }

        /// <summary>
        /// Open the connection
        /// </summary>
        public void Connect()
        {
            _Connection.Connect();
        }

        /// <summary>
        /// Send an RTC message (such as an offer) to the node
        /// </summary>
        /// <param name="message"></param>
        public void Send(RTCMessageEnvelope message)
        {
            _Connection.Send(message.Serialize());
        }

        /// <summary>
        /// Close the connection
        /// </summary>
        public void Disconnect()
        {
            if (null != _Connection && _Connection.IsAlive)
            {
                _Connection.Close();
            }
        }

        /// <summary>
        /// Close the connection
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}