using System.Collections.Concurrent;
using Cryptopia.Node.RTC.Channels;
using Cryptopia.Node.RTC.Channels.Concrete;
using Cryptopia.Node.RTC.Channels.Concrete.Config.ICE;
using Cryptopia.Node.RTC.Extensions;
using Cryptopia.Node.RTC.Messages;
using Cryptopia.Node.RTC.Signalling;
using Cryptopia.Node.Services.Logging;

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
        /// Dictionary to store node channels
        /// </summary>
        private ConcurrentDictionary<string, INodeChannel> _NodeChannels = new ConcurrentDictionary<string, INodeChannel>();

        /// <summary>
        /// Dictionary to store account channels
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentDictionary<string, IAccountChannel>> _AccountChannels = new ConcurrentDictionary<string, ConcurrentDictionary<string, IAccountChannel>>();

        #region "Node Channels"

        /// <summary>
        /// Returns true if the specified signer is associated with a known node
        /// </summary>
        /// <param name="signer">Node signer</param>
        /// <returns></returns>
        public bool IsKnownNode(string signer)
        {
            return _NodeChannels.ContainsKey(signer);
        }

        /// <summary>
        /// Returns the total number of channels to nodes
        /// </summary>
        /// <returns></returns>
        public int GetNodeChannelCount()
        {
            return _NodeChannels.Keys.Count;
        }

        /// <summary>
        /// Returns all channels to nodes
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, INodeChannel> GetNodeChannels()
        {
            // Copy the channels
            return _NodeChannels.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Returns the channel for the specified node 
        /// </summary>
        /// <param name="signer">Signer associated with the node</param></param>
        /// <returns></returns>
        public INodeChannel GetNodeChannel(string signer)
        {
            return _NodeChannels[signer];
        }

        /// <summary>
        /// Creates a new channel to the specified node
        /// </summary>
        /// <param name="signer">Signer</param></param>
        /// <param name="signalling">Signalling server</param></param>
        /// <returns></returns>
        public INodeChannel CreateNodeChannel(string signer, ISignallingService signalling)
        {
            if (null == AccountManager.Instance.Signer)
            {
                var exception = new ArgumentNullException("Signer is not set");
                LoggingService?.LogException(exception);
                throw exception;
            }

            var channel = new NodeChannel(
                true, // Polite
                false, // Not initiated by us
                LoggingService,
                signalling,
                AccountManager.Instance.Signer,
                new ExternalAccount(signer))
            .StartPeerConnection([
                new ICEServerConfig()
                {
                    Urls = "stun:stun.l.google.com:19302"
                }]);

            // Subscribe  to events
            channel.OnMessage += OnNodeChannelMessage;
            channel.OnStable += (sender, args) => channel.StartHeartbeat();
            channel.OnTimeout += (sender, args) => RemoveNodeChannel(signer, true);
            channel.OnDispose += (sender, args) => RemoveNodeChannel(signer, false);

            // Store in memory
            _NodeChannels[signer] = channel;
            return channel;
        }

        /// <summary>
        /// Removes the channel from the dictionary
        /// </summary>
        /// <param name="signer"></param>
        /// <param name="dispose"></param>
        private void RemoveNodeChannel(string signer, bool dispose)
        {
            _NodeChannels.TryRemove(signer, out var channel);
            if (dispose)
            {
                channel?.Dispose();
            }
        }

        /// <summary>
        /// Handles a node channel message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="envelope"></param>
        private void OnNodeChannelMessage(object? sender, RTCMessageEnvelope envelope)
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

        #endregion

        #region "Account Channels"

        /// <summary>
        /// Returns true if the account has at least one channel
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <returns></returns>
        public bool IsKnownAccount(string account)
        {
            return _AccountChannels.ContainsKey(account);
        }

        /// <summary>
        /// Returns true if the account has a channel with the specified signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">An ower of the smart-contact account</param>
        /// <returns></returns>
        public bool IsKnownAccountSigner(string account, string signer)
        {
            if (!IsKnownAccount(account))
            {
                return false;
            }

            return _AccountChannels[account].ContainsKey(signer);
        }

        /// <summary>
        /// Returns the total number of channels
        /// </summary>
        /// <returns></returns>
        public int GetAccountChannelCount()
        {
            return _AccountChannels.Keys.Count;
        }

        /// <summary>
        /// Returns the channels
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, IDictionary<string, IAccountChannel>> GetAccountChannels()
        {
            // Copy the channels
            return _AccountChannels.ToDictionary(
                x => x.Key, x => x.Value.ToDictionary(
                    y => y.Key, y => y.Value) as IDictionary<string, IAccountChannel>);
        }

        /// <summary>
        /// Returns the channels for the specified account
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <returns></returns>
        public IAccountChannel[] GetAccountChannels(string account)
        {
            return _AccountChannels[account].Values.ToArray();
        }

        /// <summary>
        /// Returns the channel for the specified account and signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">An ower of the smart-contact account</param></param>
        /// <returns></returns>
        public IAccountChannel GetAccountChannel(string account, string signer)
        {
            return _AccountChannels[account][signer];
        }

        /// <summary>
        /// Creates a new channel to the specified account and signer
        /// </summary>
        /// <param name="account">Registered smart-contract address</param>
        /// <param name="signer">Signer</param></param>
        /// <param name="signalling">Signalling server</param></param>
        /// <returns></returns>
        public IAccountChannel CreateAccountChannel(string account, string signer, ISignallingService signalling)
        {
            if (null == AccountManager.Instance.Signer)
            {
                var exception = new ArgumentNullException("Signer is not set");
                LoggingService?.LogException(exception);
                throw exception;
            }

            var channel = new AccountChannel(
                true, // Polite
                false, // Not initiated by us
                LoggingService,
                signalling,
                AccountManager.Instance.Signer,
                new ExternalAccount(signer),
                new RegisteredAccount(account, "Unknown"))
            .StartPeerConnection([
                new ICEServerConfig()
                {
                    Urls = "stun:stun.l.google.com:19302"
                }]);

            // Subscribe  to events
            channel.OnMessage += OnAccountChannelMessage;
            channel.OnStable += (sender, args) => channel.StartHeartbeat();
            channel.OnTimeout += (sender, args) => RemoveAccountChannel(account, signer, true);
            channel.OnDispose += (sender, args) => RemoveAccountChannel(account, signer, false);

            // Store in memory
            if (!_AccountChannels.ContainsKey(account))
            {
                _AccountChannels[account] = new ConcurrentDictionary<string, IAccountChannel>();
            }

            _AccountChannels[account][signer] = channel;
            return channel;
        }

        /// <summary>
        /// Removes the channel from the dictionary
        /// </summary>
        /// <param name="account"></param>
        /// <param name="signer"></param>
        /// <param name="dispose"></param>
        private void RemoveAccountChannel(string account, string signer, bool dispose)
        {
            if (_AccountChannels.TryGetValue(account, out var channels))
            {
                channels.TryRemove(signer, out var channel);
                if (dispose)
                {
                    channel?.Dispose();
                }

                if (channels.IsEmpty)
                {
                    _AccountChannels.TryRemove(account, out _);
                }
            }
        }

        /// <summary>
        /// Handles the channel message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="envelope"></param>
        private void OnAccountChannelMessage(object? sender, RTCMessageEnvelope envelope)
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

        #endregion

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
            var channels = _AccountChannels.Values.SelectMany(x => x.Values);
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
                    // Dispose account channels
                    foreach (var accountChannels in _AccountChannels.Values)
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
                                Console.WriteLine($"Error disposing account channel: {ex.Message}");
                            }
                        }
                    }
                    _AccountChannels.Clear();

                    // Dispose node channels
                    foreach (var channel in _NodeChannels.Values)
                    {
                        try
                        {
                            channel.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // Log the exception
                            Console.WriteLine($"Error disposing node channel: {ex.Message}");
                        }
                    }
                    _NodeChannels.Clear();

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