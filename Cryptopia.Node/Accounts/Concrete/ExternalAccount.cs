namespace Cryptopia.Node
{
    /// <summary>
    /// An account that is not registered with the on-chain account register
    /// </summary>
    public class ExternalAccount : BaseAccount
    {
        /// <summary>
        /// Static empty
        /// </summary>
        public static ExternalAccount Empty = new ExternalAccount();

        /// <summary>
        /// Public key
        /// </summary>
        public override string Address
        {
            get
            {
                return null == _address ? DEFAULT_ADDRESS : _address;
            }
        }
        private string _address;

        /// <summary>
        /// Returns true if the address is set to the default address
        /// </summary>
        public override bool IsEmpty
        {
            get
            {
                return null == _address || Address == DEFAULT_ADDRESS;
            }
        }

        /// <summary>
        /// Construct 
        /// </summary>
        private ExternalAccount()
        {
            _address = DEFAULT_ADDRESS;
        }

        /// <summary>
        /// Construct 
        /// </summary>
        /// <param name="address"></param>
        public ExternalAccount(string address)
        {
            _address = address;
        }

        /// <summary>
        /// Compare to other account
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override bool Equals(IAccount other)
        {
            return null != other && Address == other.Address;
        }

        /// <summary>
        /// Displays account username
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Address;
        }
    }
}
