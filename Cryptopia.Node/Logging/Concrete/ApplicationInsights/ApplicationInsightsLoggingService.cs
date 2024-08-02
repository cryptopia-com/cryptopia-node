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
        /// <param name="properties"></param>
        public override void Log(string message, IDictionary<string, string>? properties)
        {
            base.Log(message, properties);

            _TelemetryClient.TrackTrace(message);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public override void LogInfo(string message, IDictionary<string, string>? properties)
        {
            base.LogInfo(message, properties);

            _TelemetryClient.TrackTrace(
                message, SeverityLevel.Information, properties);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public override void LogWarning(string message, IDictionary<string, string>? properties)
        {
            base.LogWarning(message, properties);

            _TelemetryClient.TrackTrace(
                message, SeverityLevel.Warning, properties);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public override void LogError(string message, IDictionary<string, string>? properties)
        {
            base.LogError(message, properties);

            _TelemetryClient.TrackTrace(
                message, SeverityLevel.Error, properties);
        }

        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="properties"></param>
        public override void LogException(Exception ex, IDictionary<string, string>? properties)
        {
            base.LogException(ex, properties);

            _TelemetryClient.TrackException(
                ex, properties);
        }
    }
}
