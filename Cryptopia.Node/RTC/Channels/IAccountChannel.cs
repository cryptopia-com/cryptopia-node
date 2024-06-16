namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// A Channel from a node (us) to an account that is registered with 
    /// </summary>
    public interface IAccountChannel : IChannel
    {
        /// <summary>
        /// The node account (us)
        /// </summary>
        LocalAccount OriginSigner { get; }

        /// <summary>
        /// The signer of the destination account (local)
        /// </summary>
        LocalAccount DestinationSigner { get; }

        /// <summary>
        /// The registered destination account (smart-contract)
        /// </summary>
        RegisteredAccount DestinationAccount { get; }
    }
}