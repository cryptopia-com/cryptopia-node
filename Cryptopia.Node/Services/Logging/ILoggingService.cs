namespace Cryptopia.Node.Services.Logging
{
    /// <summary>
    /// Generic logging service
    /// </summary>
    public interface ILoggingService : IDisposable
    {
        // Events
        event EventHandler<LogEventArgs<string>> OnLog;
        event EventHandler<LogEventArgs<string>> OnInfo;
        event EventHandler<LogEventArgs<string>> OnWarning;
        event EventHandler<LogEventArgs<string>> OnError;
        event EventHandler<LogEventArgs<Exception>> OnException;

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        void Log(string message, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        void LogInfo(string message, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        void LogWarning(string message, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        void LogError(string message, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="properties"></param>
        void LogException(Exception ex, IDictionary<string, string>? properties = null);
    }
}