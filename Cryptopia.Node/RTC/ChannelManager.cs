using System.Collections.Concurrent;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Channel Manager
    /// 
    /// Manages the RTC (Real-Time Communication) channels in a decentralized mesh network
    /// 
    /// Responsibilities:
    /// 1) Discover public nodes on-chain that expose public endpoints
    /// 2) Connect to the public node(s) via WebSockets and set up communication channels
    /// 3) Maintain and manage RTC channels for nodes and accounts
    ///  
    /// Roles:
    /// - Nodes: Act as public endpoints in the mesh network, discovered on-chain and connected via WebSockets. 
    ///   They facilitate the communication between different accounts.
    /// - Accounts: Represent individual users (players) within the mesh network. Each account has an associated
    ///   RTC channel that allows for direct communication with other accounts or nodes.
    /// </summary>
    public class ChannelManager : Singleton<ChannelManager>, IDisposable
    {
        /// <summary>
        /// Logging service
        /// </summary>
        public ILoggingService? LoggingService;

        // Internal
        private bool _Disposed = false;
        private readonly object _DisposeLock = new object();

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
        /// Returns the total number of channels
        /// </summary>
        /// <returns></returns>
        public int GetChannelCount()
        {
            return _Channels.Keys.Count;
        }

        /// <summary>
        /// Returns the channels
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, IDictionary<string, IAccountChannel>> GetChannels()
        {
            // Copy the channels
            return _Channels.ToDictionary(
                x => x.Key, x => x.Value.ToDictionary(
                    y => y.Key, y => y.Value) as IDictionary<string, IAccountChannel>);
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
        /// <param name="signalling">Signalling server</param></param>
        /// <returns></returns>
        public IAccountChannel CreateChannel(string account, string signer, ISignallingService signalling)
        {
            if (IsKnown(account, signer))
            {
                throw new InvalidOperationException("Channel already exists");
            }

            var channel = new AccountChannel(
                true, // Polite
                false, // Not initiated by us
                LoggingService,
                signalling,
                new LocalAccount("0x"),
                new LocalAccount(signer),
                new RegisteredAccount(account, "Unknown"))
            .StartPeerConnection([
                new ICEServerConfig()
                {
                    Urls = "stun:stun.l.google.com:19302"
                }]);

            // Subscribe  to events
            channel.OnMessage += OnChannelMessage;
            channel.OnStable += (sender, args) => channel.StartHeartbeat();
            channel.OnTimeout += (sender, args) => RemoveChannel(account, signer, true);
            channel.OnDispose += (sender, args) => RemoveChannel(account, signer, false);
            
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
        /// <param name="dispose"></param>
        public void RemoveChannel(string account, string signer, bool dispose = true)
        {
            if (_Channels.TryGetValue(account, out var channels))
            {
                channels.TryRemove(signer, out var channel);
                if (dispose)
                {
                    channel?.Dispose();
                }

                if (channels.IsEmpty)
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

        /// <summary>
        /// Disposes the ChannelManager and all channels.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the ChannelManager and all channels
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }
                
            lock (_DisposeLock)
            {
                if (_Disposed)
                {
                    return;
                }

                if (disposing)
                {
                    // Dispose all channels
                    foreach (var accountChannels in _Channels.Values)
                    {
                        foreach (var channel in accountChannels.Values)
                        {
                            try
                            {
                                channel.Dispose();
                            }
                            catch (Exception ex)
                            {
                                // Log the exception
                                Console.WriteLine($"Error disposing channel: {ex.Message}");
                            }
                        }
                    }
                    _Channels.Clear();

                    // Dispose the logging service
                    if (null != LoggingService)
                    {
                        LoggingService.Dispose();
                    }
                }

                _Disposed = true;
            }
        }
    }
}