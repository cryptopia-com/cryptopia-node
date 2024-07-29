namespace Cryptopia.Node
{
    /// <summary>
    /// Base logging service
    /// </summary>
    public class AppLoggingService : ILoggingService
    {
        /// <summary>
        /// True if logging is enabled
        /// </summary>
        public bool WriteToConsole;

        // Events
        public event EventHandler<string>? OnLog;
        public event EventHandler<string>? OnInfo;
        public event EventHandler<string>? OnWarning;
        public event EventHandler<string>? OnError;

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        public virtual void Log(string message)
        {
            if (WriteToConsole)
            {
                Console.WriteLine(message);
            }

            OnLog?.Invoke(this, message);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        public virtual void LogInfo(string message)
        {
            if (WriteToConsole)
            {
                Console.WriteLine($"Info: {message}");
            }

            OnInfo?.Invoke(this, message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        public virtual void LogWarning(string message)
        {
            if (WriteToConsole)
            {
                Console.WriteLine($"Warning: {message}");
            }

            OnWarning?.Invoke(this, message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        public virtual void LogError(string message)
        {
            if (WriteToConsole)
            {
                Console.WriteLine($"Error: {message}");
            }

            OnError?.Invoke(this, message);
        }

        /// <summary>
        /// Can be overridden to dispose of resources
        /// </summary>
        public virtual void Dispose()
        {
            // Nothing to dispose
        }
    }
}
