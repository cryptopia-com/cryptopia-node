namespace Cryptopia.Node
{
    /// <summary>
    /// Blockchain account
    /// </summary>
    public interface IAccount
    {
        /// <summary>
        /// Public address
        /// </summary>
        string Address { get; }

        /// <summary>
        /// Returns true if the address is set to 0x
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Compare to other account
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        bool Equals(IAccount other);
    }
}
