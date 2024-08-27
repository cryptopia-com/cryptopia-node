using Cryptopia.Node.Services.Logging;

namespace Cryptopia.Node.RTC.Signalling.WebSocketSharp
{
    public class WebSocketSharpSignallingServiceFactory : ISignallingServiceFactory
    {
        private readonly ILoggingService _loggingService;

        public WebSocketSharpSignallingServiceFactory(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public ISignallingService Create(string endpoint)
        {
            return new WebSocketSharpSignallingService(endpoint, _loggingService);
        }
    }
}
