namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Configuration for an RTC channel
    /// </summary>
    public interface IChannelConfig
    {
        /// <summary>
        /// If set to true, the channel will be polite
        /// </summary>
        bool Polite { get; }

        /// <summary>
        /// If set to true, the channel was initiated by us
        /// </summary>
        bool InitiatedByUs { get; }

        /// <summary>
        /// The ICE servers to use for the channel
        /// </summary>
        IICEServerConfig[] IceServers { get; set; }
    }
}
