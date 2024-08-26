using Cryptopia.Node.RTC.Channels.Config.ICE;

namespace Cryptopia.Node.RTC.Channels.Concrete.Config.ICE
{
    /// <summary>
    /// ICE Server configuration
    /// </summary>
    public class ICEServerConfig : IICEServerConfig
    {
        /// <summary>
        /// Urls of the ICE servers
        /// </summary>
        public required string Urls { get; set; }
    }
}
