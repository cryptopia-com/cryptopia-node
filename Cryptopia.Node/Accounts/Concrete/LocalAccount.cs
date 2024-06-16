namespace Cryptopia.Node
{
    /// <summary>
    /// A local account
    /// </summary>
    public class LocalAccount : BaseAccount
    {
        /// <summary>
        /// Static empty
        /// </summary>
        public static LocalAccount Empty = new LocalAccount();

        /// <summary>
        /// Public key
        /// </summary>
        public override string Address
        {
            get
            {
                return null == _Address ? DEFAULT_ADDRESS : _Address;
            }
        }
        private string _Address;

        /// <summary>
        /// Returns true if the address is set to the default address
        /// </summary>
        public override bool IsEmpty
        {
            get
            {
                return null == _Address || Address == DEFAULT_ADDRESS;
            }
        }

        /// <summary>
        /// Construct 
        /// </summary>
        private LocalAccount()
        {
            _Address = DEFAULT_ADDRESS;
        }

        /// <summary>
        /// Construct 
        /// </summary>
        /// <param name="address"></param>
        public LocalAccount(string address)
        {
            _Address = address;
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
