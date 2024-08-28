using Cryptopia.Node.Services.Logging;

namespace Cryptopia.Node.RTC.Signalling.WebSocketSharp
{
    /// <summary>
    /// Factory for creating WebSocketSharpSignallingService instances
    /// </summary>
    public class WebSocketSharpSignallingServiceFactory : ISignallingServiceFactory
    {
        /// <summary>
        /// Reference to the logging service
        /// </summary>
        private readonly ILoggingService _loggingService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggingService"></param>
        public WebSocketSharpSignallingServiceFactory(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        /// <summary>
        /// Creates a new WebSocketSharpSignallingService instance
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public ISignallingService Create(string endpoint)
        {
            return new WebSocketSharpSignallingService(endpoint, _loggingService);
        }
    }
}
