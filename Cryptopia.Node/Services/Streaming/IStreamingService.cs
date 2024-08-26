namespace Cryptopia.Node.Services.Streaming
{
    /// <summary>
    /// Service that streams data to the cosole
    /// </summary>
    public interface IStreamingService : IDisposable
    {
        /// <summary>
        /// Starts the streaming service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the streaming service
        /// </summary>
        void Stop();
    }
}
