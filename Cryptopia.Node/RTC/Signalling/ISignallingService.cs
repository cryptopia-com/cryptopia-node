namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Service for RTC signalling with clients
    /// </summary>
    public interface ISignallingService
    {
        /// <summary>
        /// True if the connection is open
        /// </summary>
        public bool IsOpen { get; }

        // Events
        event EventHandler OnOpen;
        event EventHandler<RTCMessageEnvelope> OnReceiveMessage;

        /// <summary>
        /// Open the connection
        /// </summary>
        void Connect();

        /// <summary>
        /// Send an RTC message (such as an offer) to the node
        /// </summary>
        /// <param name="message"></param>
        void Send(RTCMessageEnvelope message);

        /// <summary>
        /// Close the connection
        /// </summary>
        void Disconnect();
    }
}
