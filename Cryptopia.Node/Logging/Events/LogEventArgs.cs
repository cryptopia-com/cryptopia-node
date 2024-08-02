namespace Cryptopia.Node
{
    /// <summary>
    /// Log event arguments
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LogEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Required subject
        /// </summary>
        public required T Value { get; set; }

        /// <summary>
        /// Dictionary of properties
        /// </summary>
        public IDictionary<string, string>? Properties { get; set; }
    }
}