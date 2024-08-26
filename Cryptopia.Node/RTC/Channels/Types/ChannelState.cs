namespace Cryptopia.Node.RTC.Channels.Types
{
    /// <summary>
    /// The state of a channel
    /// </summary>
    public enum ChannelState
    {
        Initiating,
        Connecting,
        Signalling,
        Rejected,
        Failed,
        Open,
        Closing,
        Closed,
        Disposing,
        Disposed
    }
}
