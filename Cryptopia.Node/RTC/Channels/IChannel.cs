namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Returns true if the channel is stable
        /// </summary>
        bool IsStable { get; }

        /// <summary>
        /// Gets the current state of the channel
        /// </summary>
        ChannelState State { get; }

        /// <summary>
        /// Gets a value indicating whether the channel is polite
        /// </summary>
        bool IsPolite { get; }

        /// <summary>
        /// Gets a value indicating whether the channel was initiated by us
        /// </summary>
        bool IsInitiatedByUs { get; }

        /// <summary>
        /// Heartbeat interval in milliseconds
        /// 
        /// The interval at which the channel will send ping messages to the 
        /// remote peer in order to ensure the connection is still alive
        /// </summary>
        double HeartbeatInterval { get; }

        /// <summary>
        /// Heartbeat timeout in milliseconds
        /// 
        /// The maximum timeout that the channel will tolerate before self-terminating
        /// </summary>
        double HeartbeatTimeout { get; }

        /// <summary>
        /// Max latency in milliseconds (zero means no latency)
        /// 
        /// The maximum latency that the channel will tolerate before self-terminating
        /// </summary>
        double Latency { get; }

        /// <summary>
        /// Max latency in milliseconds (zero means no latency)
        /// 
        /// Anything above this value will be considered high latency 
        /// </summary>
        double MaxLatency { get; }

        /// <summary>
        /// Occurs when the channel is opened
        /// </summary>
        event EventHandler OnOpen;

        /// <summary>
        /// Occurs when the state of the channel changes
        /// </summary>
        event EventHandler<ChannelState> OnStateChange;

        /// <summary>
        /// Occurs when a message is received
        /// </summary>
        event EventHandler<RTCMessageEnvelope> OnMessage;

        /// <summary>
        /// Occurs when the latency of the channel changes
        /// </summary>
        event EventHandler<double> OnLatency;

        /// <summary>
        /// Occurs when the channel has high latency
        /// </summary>
        event EventHandler OnHighLatency;

        /// <summary>
        /// Occurs when the channel encounters a timeout
        /// </summary>
        event EventHandler OnTimeout;

        /// <summary>
        /// Occurs when the channel is disposed
        /// </summary>
        event EventHandler OnDispose;

        /// <summary>
        /// Opens the channel
        /// </summary>
        /// <returns>Task that represents the asynchronous operation</returns>
        Task OpenAsync();

        /// <summary>
        /// Accepts an SDP offer to establish the channel
        /// </summary>
        /// <param name="offer">The SDP offer to accept</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        Task AcceptAsync(SDPInfo offer);

        /// <summary>
        /// Rejects an SDP offer
        /// </summary>
        /// <param name="offer">The SDP offer to reject</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        Task RejectAsync(SDPInfo offer);

        /// <summary>
        /// Closes the channel
        /// </summary>
        /// <returns>Task that represents the asynchronous operation</returns>
        Task CloseAsync();

        /// <summary>
        /// Sends a message over the channel
        /// </summary>
        /// <param name="message">The message to send</param>
        void Send(string message);
    }
}
