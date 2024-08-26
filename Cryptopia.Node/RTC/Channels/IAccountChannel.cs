namespace Cryptopia.Node.RTC.Channels
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
        ExternalAccount DestinationSigner { get; }

        /// <summary>
        /// The registered destination account (smart-contract)
        /// </summary>
        RegisteredAccount DestinationAccount { get; }
    }
}