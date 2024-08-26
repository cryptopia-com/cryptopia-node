using Cryptopia.Node.RTC.Channels.Config;
using Cryptopia.Node.RTC.Channels.Config.ICE;

namespace Cryptopia.Node.RTC.Channels.Concrete.Config
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
        /// Heartbeat interval in milliseconds
        /// 
        /// The interval at which the channel will send ping messages to the 
        /// remote peer in order to ensure the connection is still alive
        /// </summary>
        public uint? HeartbeatInterval { get; set; }

        /// <summary>
        /// Max latency in milliseconds (zero means no latency)
        /// 
        /// The maximum latency that the channel will tolerate before self-terminating
        /// </summary>
        public uint? MaxLatency { get; set; }

        /// <summary>
        /// The ICE servers to use for the channel
        /// </summary>
        public required IICEServerConfig[] IceServers { get; set; }
    }
}
