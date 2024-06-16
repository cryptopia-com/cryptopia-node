namespace Cryptopia.Node
{
    /// <summary>
    /// An account that is registered with the on-chain account register
    /// </summary>
    public class RegisteredAccount : BaseAccount
    {
        /// <summary>
        /// Static empty
        /// </summary>
        public static RegisteredAccount Empty = new RegisteredAccount();

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
        /// Account username
        /// </summary>
        public string Username
        {
            get
            {
                return null == _Username ? string.Empty : _Username;
            }
        }
        private string _Username;

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
        private RegisteredAccount()
        {
            _Address = DEFAULT_ADDRESS;
            _Username = string.Empty;
        }

        /// <summary>
        /// Construct 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="username"></param>
        public RegisteredAccount(string address, string username)
        {
            _Address = address;
            _Username = username;
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
            return Username;
        }
    }
}
