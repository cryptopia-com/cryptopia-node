using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Cryptopia.Node.ApplicationInsights
{
    /// <summary>
    /// Logging service that uses Application Insights (azure)
    /// </summary>
    internal class ApplicationInsightsLoggingService : AppLoggingService
    {
        /// <summary>
        /// Instance of the telemetry client
        /// </summary>
        private TelemetryClient _TelemetryClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="telemetryClient"></param>
        public ApplicationInsightsLoggingService(TelemetryClient telemetryClient) 
            : base()
        {
            _TelemetryClient = telemetryClient;
        }

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        public override void Log(string message)
        {
            base.Log(message);

            _TelemetryClient.TrackTrace(message);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        public override void LogInfo(string message)
        {
            base.LogInfo(message);

            _TelemetryClient.TrackTrace(message, SeverityLevel.Information);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        public override void LogWarning(string message)
        {
            base.LogWarning(message);

            _TelemetryClient.TrackTrace(message, SeverityLevel.Warning);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        public override void LogError(string message)
        {
            base.LogError(message);

            _TelemetryClient.TrackTrace(message, SeverityLevel.Error);
        }
    }
}
