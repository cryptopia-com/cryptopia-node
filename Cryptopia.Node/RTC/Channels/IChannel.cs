namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public interface IChannel
    {
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
        /// Occurs when the channel is opened
        /// </summary>
        event EventHandler OnOpen;

        /// <summary>
        /// Occurs when the state of the channel changes
        /// </summary>
        event EventHandler<ChannelState> OnStateChange;

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
        /// <returns>Task that represents the asynchronous operation</returns>
        Task RejectAsync();

        /// <summary>
        /// Closes the channel
        /// </summary>
        void Close();

        /// <summary>
        /// Sends a message over the channel
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        void Send(string message);
    }
}
