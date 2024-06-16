using System.Collections.Concurrent;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// 
    /// </summary>
    public class ChannelManager : Singleton<ChannelManager>
    {
        /// <summary>
        /// Dictionary to store account channels
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentDictionary<string, IAccountChannel>> _Channels = new ConcurrentDictionary<string, ConcurrentDictionary<string, IAccountChannel>>();

        /// <summary>
        /// Returns true if the account has at least one channel
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <returns></returns>
        public bool IsKnown(string account)
        { 
            return _Channels.ContainsKey(account);
        }

        /// <summary>
        /// Returns true if the account has a channel with the specified signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">An ower of the smart-contact account</param>
        /// <returns></returns>
        public bool IsKnown(string account, string signer)
        {
            if (!IsKnown(account))
            {
                return false;
            }

            return _Channels[account].ContainsKey(signer);
        }

        /// <summary>
        /// Returns the channels for the specified account
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <returns></returns>
        public IAccountChannel[] GetChannels(string account)
        {
            return _Channels[account].Values.ToArray();
        }

        /// <summary>
        /// Returns the channel for the specified account and signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">An ower of the smart-contact account</param></param>
        /// <returns></returns>
        public IAccountChannel GetChannel(string account, string signer)
        {
            return _Channels[account][signer];
        }

        /// <summary>
        /// Creates a new channel to the specified account and signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">Signer</param></param>
        /// <retu
        public IAccountChannel CreateChannel(string account, string signer, ISignallingService signalling)
        {
            var channel = new AccountChannel(
                new ChannelConfig() 
                { 
                    Polite = true,
                    IceServers =
                    [
                        new ICEServerConfig() 
                        {
                            Urls = "stun:stun.l.google.com:19302"
                        }
                    ]
                },
                new RTCLoggingService(),
                signalling,
                new LocalAccount("0x"),
                new LocalAccount(signer),
                new RegisteredAccount(account, "Unknown"));

            // Store in memory
            if (!_Channels.ContainsKey(account))
            {
                _Channels[account] = new ConcurrentDictionary<string, IAccountChannel>();
            }

            _Channels[account][signer] = channel;

            return channel;
        }
    }
}
