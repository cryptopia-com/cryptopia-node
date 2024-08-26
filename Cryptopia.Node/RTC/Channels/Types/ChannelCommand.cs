namespace Cryptopia.Node.RTC.Channels.Types
{
    /// <summary>
    /// Commands to be sent over the command channel
    /// </summary>
    public enum ChannelCommand
    {
        Ping,
        Pong,
        Close,
        Dispose
    }
}
