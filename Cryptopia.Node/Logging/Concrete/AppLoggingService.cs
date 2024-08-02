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
        public event EventHandler<LogEventArgs<string>>? OnLog;
        public event EventHandler<LogEventArgs<string>>? OnInfo;
        public event EventHandler<LogEventArgs<string>>? OnWarning;
        public event EventHandler<LogEventArgs<string>>? OnError;
        public event EventHandler<LogEventArgs<Exception>>? OnException;

        /// <summary>
        /// Logs a generic message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public virtual void Log(string message, IDictionary<string, string>? properties = null)
        {
            if (WriteToConsole)
            {
                if (null == properties || properties.Count == 0)
                {
                    Console.WriteLine(message);
                }
                else
                {
                    Console.WriteLine($"{message} - {string.Join(", ", properties.Select(p => $"{p.Key}: {p.Value}"))}");
                }
            }

            OnLog?.Invoke(this, new LogEventArgs<string>
            { 
                Value = message,
                Properties = properties
            });
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public virtual void LogInfo(string message, IDictionary<string, string>? properties = null)
        {
            if (WriteToConsole)
            {
                if (null == properties || properties.Count == 0)
                {
                    Console.WriteLine($"Info: {message}");
                }
                else
                {
                    Console.WriteLine($"Info: {message} - {string.Join(", ", properties.Select(p => $"{p.Key}: {p.Value}"))}");
                }
            }

            OnInfo?.Invoke(this, new LogEventArgs<string>
            {
                Value = message,
                Properties = properties
            });
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public virtual void LogWarning(string message, IDictionary<string, string>? properties = null)
        {
            if (WriteToConsole)
            {
                if (null == properties || properties.Count == 0)
                {
                    Console.WriteLine($"Warning: {message}");
                }
                else
                {
                    Console.WriteLine($"Warning: {message} - {string.Join(", ", properties.Select(p => $"{p.Key}: {p.Value}"))}");
                }
            }

            OnWarning?.Invoke(this, new LogEventArgs<string>
            {
                Value = message,
                Properties = properties
            });
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        public virtual void LogError(string message, IDictionary<string, string>? properties = null)
        {
            if (WriteToConsole)
            {
                if (null == properties || properties.Count == 0)
                {
                    Console.WriteLine($"Error: {message}");
                }
                else
                {
                    Console.WriteLine($"Error: {message} - {string.Join(", ", properties.Select(p => $"{p.Key}: {p.Value}"))}");
                }
            }

            OnError?.Invoke(this, new LogEventArgs<string>
            {
                Value = message,
                Properties = properties
            });
        }

        /// <summary>
        /// Logs an exception as an error
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="properties"></param>
        public virtual void LogException(Exception ex, IDictionary<string, string>? properties = null)
        {
            if (WriteToConsole)
            {
                if (null == properties || properties.Count == 0)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
                else
                {
                    Console.WriteLine($"Exception: {ex.Message} - {string.Join(", ", properties.Select(p => $"{p.Key}: {p.Value}"))}");
                }
            }

            OnException?.Invoke(this, new LogEventArgs<Exception>
            {
                Value = ex,
                Properties = properties
            });
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
