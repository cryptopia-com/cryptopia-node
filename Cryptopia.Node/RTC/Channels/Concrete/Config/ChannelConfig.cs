namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// RTC Channel configuration
    /// </summary>
    public class ChannelConfig : IChannelConfig
    {
        /// <summary>
        /// If set to true, the channel will be polite
        /// </summary>
        public bool Polite { get; set; }

        /// <summary>
        /// If set to true, the channel was initiated by us
        /// </summary>
        public bool InitiatedByUs { get; set; }

        /// <summary>
        /// The ICE servers to use for the channel
        /// </summary>
        public required IICEServerConfig[] IceServers { get; set; }
    }
}
