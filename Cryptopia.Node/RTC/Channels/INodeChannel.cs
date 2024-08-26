namespace Cryptopia.Node.RTC.Channels
{
    /// <summary>
    /// A Channel from a node (us) to another node (them)
    /// </summary>
    public interface INodeChannel : IChannel
    {
        /// <summary>
        /// The node account (us)
        /// </summary>
        LocalAccount OriginSigner { get; }

        /// <summary>
        /// The signer of the destination account (them)
        /// </summary>
        ExternalAccount DestinationSigner { get; }
    }
}