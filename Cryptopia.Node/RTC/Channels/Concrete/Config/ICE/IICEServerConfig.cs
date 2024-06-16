namespace Cryptopia.Node.RTC
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
