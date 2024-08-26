namespace Cryptopia.Node
{
    /// <summary>
    /// Base blockchain account
    /// </summary>
    public abstract class BaseAccount : IAccount, IDisposable
    {
        /// <summary>
        /// Default address
        /// </summary>
        public const string DEFAULT_ADDRESS = "0x0000000000000000000000000000000000000000";

        /// <summary>
        /// Public key
        /// </summary>
        public abstract string Address { get; }

        /// <summary>
        /// Returns true if the address is set to the default address
        /// </summary>
        public abstract bool IsEmpty { get; }

        /// <summary>
        /// Compare to other account
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public abstract bool Equals(IAccount other);

        /// <summary>
        /// Dispose
        /// </summary>
        public virtual void Dispose() { }
    }
}
