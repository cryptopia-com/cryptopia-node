namespace Cryptopia.Node.RTC.Channels.Config.ICE
{
    /// <summary>
    /// Configuration for ICE servers
    /// </summary>
    public interface IICEServerConfig
    {
        /// <summary>
        /// Urls of the ICE servers
        /// </summary>
        string Urls { get; }
    }
}
