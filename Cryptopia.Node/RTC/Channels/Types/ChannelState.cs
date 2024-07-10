namespace Cryptopia.Node.RTC
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
