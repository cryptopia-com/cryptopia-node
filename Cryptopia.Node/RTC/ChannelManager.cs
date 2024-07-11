using System.Collections.Concurrent;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Manages the channels to the node
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
        /// <returns></returns>
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

            // Subscribe  to events
            channel.OnMessage += OnChannelMessage;
            channel.OnDispose += (sender, args) => RemoveChannel(account, signer);

            // Store in memory
            if (!_Channels.ContainsKey(account))
            {
                _Channels[account] = new ConcurrentDictionary<string, IAccountChannel>();
            }

            _Channels[account][signer] = channel;
            return channel;
        }

        /// <summary>
        /// Removes the channel from the dictionary
        /// </summary>
        /// <param name="account"></param>
        /// <param name="signer"></param>
        private void RemoveChannel(string account, string signer)
        {
            if (_Channels.TryGetValue(account, out var accountChannels))
            {
                accountChannels.TryRemove(signer, out _);
                if (accountChannels.IsEmpty)
                {
                    _Channels.TryRemove(account, out _);
                }
            }
        }

        /// <summary>
        /// Handles the channel message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="envelope"></param>
        private void OnChannelMessage(object? sender, RTCMessageEnvelope envelope)
        {
            switch (envelope.Payload.Type)
            {
                case RTCMessageType.Relay:
                    RelayChannelMessage(envelope);
                    break;
                case RTCMessageType.Broadcast:
                    BroadcastChannelMessage(envelope);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Relays the message to the specified channel
        /// </summary>
        /// <param name="envelope"></param>
        private void RelayChannelMessage(RTCMessageEnvelope envelope)
        {
            // Not implemented
        }

        /// <summary>
        /// Broadcasts the message to all channels except the sender
        /// </summary>
        /// <param name="envelope"></param>
        private void BroadcastChannelMessage(RTCMessageEnvelope envelope)
        {
            var channels = _Channels.Values.SelectMany(x => x.Values);
            foreach (var channel in channels)
            {
                // Exclude the sender
                if (channel.DestinationAccount.Address == envelope.Sender.Account)
                {
                    continue;
                }

                try 
                { 
                    channel.Send(envelope.Serialize());
                }
                catch (Exception)
                {
                    // Log
                }
            }
        }
    }
}