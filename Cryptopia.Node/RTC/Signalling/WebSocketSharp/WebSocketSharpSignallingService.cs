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

        // Lock for thread safety
        private readonly object _lock = new object();

        // Internal
        private WebSocket _Connection;

        // Events
        public event EventHandler? OnOpen;
        public event EventHandler<RTCMessageEnvelope>? OnReceiveMessage;

        // Queue for messages to be sent when connection is open
        private readonly Queue<RTCMessageEnvelope> _messageQueue = new Queue<RTCMessageEnvelope>();

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="loggingService"></param>
        public WebSocketSharpSignallingService(string endpoint, ILoggingService loggingService)
        {
            _Connection = new WebSocket(endpoint);

            // Handle connection opening
            _Connection.OnOpen += (sender, e) =>
            {
                lock (_lock)
                {
                    IsOpen = true;
                    OnOpen?.Invoke(this, e);

                    // Send any queued messages
                    while (_messageQueue.Count > 0)
                    {
                        var queuedMessage = _messageQueue.Dequeue();
                        _Connection.Send(queuedMessage.Serialize());
                    }
                }
            };

            // Handle incoming messages
            _Connection.OnMessage += (sender, e) =>
            {
                OnReceiveMessage?.Invoke(this, e.Data.DeserializeRTCMessage());
            };

            // Handle errors
            _Connection.OnError += (sender, e) =>
            {
                loggingService.LogError(e.Message);
            };

            // Handle connection closure
            _Connection.OnClose += (sender, e) =>
            {
                lock (_lock)
                {
                    IsOpen = false;
                }
            };
        }

        /// <summary>
        /// Open the connection
        /// </summary>
        public void Connect()
        {
            lock (_lock)
            {
                _Connection.Connect();
            }
        }

        /// <summary>
        /// Send an RTC message (such as an offer) to the node
        /// </summary>
        /// <param name="message"></param>
        public void Send(RTCMessageEnvelope message)
        {
            lock (_lock)
            {
                if (IsOpen)
                {
                    _Connection.Send(message.Serialize());
                }
                else
                {
                    _messageQueue.Enqueue(message);
                }
            }
        }

        /// <summary>
        /// Close the connection
        /// </summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                if (_Connection != null && _Connection.IsAlive)
                {
                    _Connection.Close();
                }

                IsOpen = false;
            }
        }

        /// <summary>
        /// Dispose the service
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
