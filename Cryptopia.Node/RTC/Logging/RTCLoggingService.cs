namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// RTC logging service
    /// </summary>
    public class RTCLoggingService : ILoggingService
    {
        // Events
        public event EventHandler<string>? OnLog;
        public event EventHandler<string>? OnInfo;
        public event EventHandler<string>? OnWarning;
        public event EventHandler<string>? OnError;

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        public void Log(string message)
        {
            Console.WriteLine(message);
            OnLog?.Invoke(this, message);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        public void LogInfo(string message)
        {
            Console.WriteLine($"Info: {message}");
            OnInfo?.Invoke(this, message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        public void LogWarning(string message)
        {
            Console.WriteLine($"Warning: {message}");
            OnWarning?.Invoke(this, message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        public void LogError(string message)
        {
            Console.WriteLine($"Error: {message}");
            OnError?.Invoke(this, message);
        }
    }
}
