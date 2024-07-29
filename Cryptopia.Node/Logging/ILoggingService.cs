namespace Cryptopia.Node
{
    /// <summary>
    /// Generic logging service
    /// </summary>
    public interface ILoggingService : IDisposable
    {
        // Events
        event EventHandler<string> OnLog;
        event EventHandler<string> OnInfo;
        event EventHandler<string> OnWarning;
        event EventHandler<string> OnError;

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        void Log(string message);

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        void LogError(string message);
    }
}