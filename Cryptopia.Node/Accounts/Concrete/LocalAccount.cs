using Nethereum.Web3.Accounts;
using System.Security;

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
        /// Private key
        /// </summary>
        public SecureString? PrivateKey
        {
            get
            {
                return _PrivateKey;
            }
        }
        private SecureString? _PrivateKey;

        /// <summary>
        /// Account index in nmemonic
        /// </summary>
        public int Index
        {
            get
            {
                return _Index;
            }
        }
        private int _Index;

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
        /// Returns true if the private key is null
        /// </summary>
        public bool IsLocked
        {
            get
            {
                if (null == _PrivateKey)
                {
                    return true;
                }

                bool isDisposed = false;
                try
                {
                    if (_PrivateKey.Length == 0)
                    {
                        return true;
                    }
                }
                catch (ObjectDisposedException)
                {
                    isDisposed = true;
                }

                return isDisposed;
            }
        }

        /// <summary>
        /// Construct 
        /// 
        /// Require address
        /// </summary>
        /// <param name="address"></param>
        private LocalAccount()
        {
            _Index = -1;
            _Address = DEFAULT_ADDRESS;
            _PrivateKey = null;
        }

        /// <summary>
        /// Construct 
        /// </summary>
        /// <param name="address"></param>
        public LocalAccount(string address)
        {
            _Address = address;
            _Index = -1;
            _PrivateKey = null;
        }

        /// <summary>
        /// Construct
        /// </summary>
        /// <param name="privateKey"></param>
        /// <param name="index"></param>
        public LocalAccount(SecureString privateKey, int index)
        {
            _Address = GetAccount(privateKey).Address;
            _PrivateKey = privateKey;
            _Index = index;
        }

        /// <summary>
        /// Removes private key
        /// </summary>
        public void Lock()
        {
            _PrivateKey?.Dispose();
            _PrivateKey = null;
        }

        /// <summary>
        /// TODO: Frank: Implement
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public string Sign(string data)
        {
            // Sign using sign extension and privateKey
            return data;
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

        /// <summary>
        /// Dispose private key
        /// </summary>
        public override void Dispose() 
        {
            _PrivateKey?.Dispose();
            _PrivateKey = null;
        }

        /// <summary>
        /// Retrieve the account from privateKey
        /// </summary>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        protected Account GetAccount(SecureString privateKey)
        {
            return new Account(privateKey.ToPlainString());
        }
    }
}
