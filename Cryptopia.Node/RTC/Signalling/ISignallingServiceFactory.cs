namespace Cryptopia.Node.RTC.Signalling
{
    /// <summary>
    /// Factory for creating signalling services
    /// </summary>
    public interface ISignallingServiceFactory
    {
        /// <summary>
        /// Create a signalling service
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        ISignallingService Create(string endpoint);
    }
}
