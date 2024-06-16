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
        Open,
        Closed,
        Failed
    }
}
