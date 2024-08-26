namespace Cryptopia.Node
{
    /// <summary>
    /// Account Manager
    /// 
    /// Responsible for managing the local account used for signing and verifying within the node
    /// </summary>
    public class AccountManager : Singleton<AccountManager>, IDisposable
    {
        /// <summary>
        /// Node signs and verifies with this account
        /// </summary>
        public LocalAccount? Signer
        {
            set
            {
                if (null != _Signer)
                {
                    _Signer.Dispose();
                }

                _Signer = value;
            }
            get
            {
                return _Signer;
            }
        }
        private LocalAccount? _Signer;

        /// <summary>
        /// Returns true if `account` belongs to the signer
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public bool IsSigner(string account)
        {
            return null != _Signer
                && _Signer.Address.Equals(account);
        }

        /// <summary>
        /// Disposes signer 
        /// </summary>
        public void Dispose()
        {
            _Signer?.Dispose();
        }
    }
}
