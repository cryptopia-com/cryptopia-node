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
        /// Heartbeat interval in milliseconds
        /// 
        /// The interval at which the channel will send ping messages to the 
        /// remote peer in order to ensure the connection is still alive
        /// </summary>
        uint? HeartbeatInterval { get; }

        /// <summary>
        /// Max latency in milliseconds (zero means no latency)
        /// 
        /// The maximum latency that the channel will tolerate before self-terminating
        /// </summary>
        uint? MaxLatency { get; }

        /// <summary>
        /// The ICE servers to use for the channel
        /// </summary>
        IICEServerConfig[] IceServers { get; }
    }
}
