using SIPSorcery.Net;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public abstract class BaseChannel : IChannel
    {
        /// <summary>
        /// Returns true if the channel is stable
        /// </summary>
        public bool IsStable
        {
            get
            {
                lock (_Lock)
                {
                    return _IsStable;
                }
            }
            private set
            {
                var shouldNotify = false;
                lock (_Lock)
                {
                    SetStable(value,
                        out shouldNotify);
                }

                if (shouldNotify)
                {
                    OnStable();
                }
            }
        }
        private bool _IsStable = false;

        /// <summary>
        /// Gets the current state of the channel
        /// </summary>
        public ChannelState State
        {
            get
            {
                lock (_Lock)
                {
                    return _State;
                }
            }
            protected set
            {
                bool shouldNotifyChane, shouldNotifyOpen;
                lock (_Lock)
                {
                    SetState(value,
                        out shouldNotifyChane,
                        out shouldNotifyOpen);
                }

                // Notify change
                if (shouldNotifyChane)
                {
                    OnStateChange?.Invoke(this, value);
                }

                // Notify open
                if (shouldNotifyOpen)
                {
                    OnOpen?.Invoke(this, new EventArgs());
                }
            }
        }
        private ChannelState _State;

        /// <summary>
        /// Gets a value indicating whether the channel is polite
        /// </summary>
        public bool IsPolite { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the channel was initiated by us
        /// </summary>
        public bool IsInitiatedByUs { get; private set; }

        // Services
        protected ILoggingService LoggingService { get; private set; }
        protected ISignallingService SignallingService { get; private set; }

        // Internal
        private readonly object _Lock = new object();
        private RTCDataChannel? _DataChannel;
        private RTCDataChannel? _CommandChannel;
        private RTCPeerConnection? _PeerConnection;

        // Constants
        private const string DATA_CHANNEL_LABEL = "data";
        private const string COMMAND_CHANNEL_LABEL = "command";

        // Events
        public event EventHandler? OnOpen;
        public event EventHandler<ChannelState>? OnStateChange;
        public event EventHandler? OnDispose;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="loggingService"></param>
        /// <param name="signallingService"></param>
        public BaseChannel(IChannelConfig config, ILoggingService loggingService, ISignallingService signallingService)
        {
            LoggingService = loggingService;
            SignallingService = signallingService;

            IsPolite = config.Polite;
            IsInitiatedByUs = config.InitiatedByUs;

            var peerConfig = new RTCConfiguration();
            var iceServers = new List<RTCIceServer>();
            for (int i = 0; i < config.IceServers.Length; i++)
            {
                iceServers.Add(new RTCIceServer()
                {
                    urls = config.IceServers[i].Urls,
                });
            }

            peerConfig.iceServers = iceServers;

            _PeerConnection = new RTCPeerConnection(peerConfig);
            _PeerConnection.onicecandidate += OnOnIceCandidate;
            _PeerConnection.oniceconnectionstatechange += OnIceConnectionChange;
            _PeerConnection.ondatachannel += (channel) =>
            {
                if (channel == null || string.IsNullOrEmpty(channel.label))
                {
                    loggingService.LogError("Received a data channel with no label");
                    return;
                }

                switch (channel.label)
                {
                    case DATA_CHANNEL_LABEL:
                        SetDataChannel(channel);
                        break;

                    case COMMAND_CHANNEL_LABEL:
                        SetCommandChannel(channel);
                        break;

                    default:
                        loggingService.LogError($"Received an unknown channel: {channel.label}");
                        break;
                }
            };
        }

        #region "Methods"

        /// <summary>
        /// Connects to the signalling server
        /// </summary>
        /// <returns></returns>
        private async Task ConnectAsync()
        {
            // Indicate connecting started
            LoggingService.LogInfo("Connecting");
            State = ChannelState.Connecting;

            // Connect to signalling server
            if (!SignallingService.IsOpen)
            {
                SignallingService.Connect();

                // Set the timeout duration (in seconds)
                var timeout = DateTime.Now + TimeSpan.FromSeconds(10); // TODO: From config
                while (!SignallingService.IsOpen && DateTime.Now < timeout)
                {
                    await Task.Delay(100);
                }

                if (!SignallingService.IsOpen)
                {
                    State = ChannelState.Failed;
                    var exception = new InvalidOperationException("Connection timeout");
                    LoggingService.LogError(exception.Message);
                    throw exception;
                }
            }
        }

        /// <summary>s
        /// Opens the channel
        /// </summary>
        /// <returns>Task that represents the asynchronous operation</returns>
        public async Task OpenAsync()
        {
            await ConnectAsync();
        }

        /// <summary>
        /// Accepts an SDP offer to establish the channel
        /// </summary>
        /// <param name="offer">The SDP offer to accept</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        public async Task AcceptAsync(SDPInfo offer)
        {
            if (IsInitiatedByUs)
            {
                var exception = new InvalidOperationException("Channel initiated by us");
                LoggingService.LogError(exception.Message);
                throw exception;
            }

            if (State != ChannelState.Initiating)
            {
                var exception = new InvalidOperationException("Can only accept offers in the iniating state");
                LoggingService.LogError(exception.Message);
                throw exception;
            }

            // Connect to signalling server
            await ConnectAsync();

            // Indicate signalling started
            bool shouldNotifyChange;
            lock (_Lock)
            {
                if (_State != ChannelState.Connecting)
                {
                    LoggingService.LogWarning($"Expected {ChannelState.Connecting} state instead of {_State} state");
                    return;
                }

                SetState(
                    ChannelState.Signalling,
                    out shouldNotifyChange,
                    out bool shouldNotifyOpen);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnStateChange?.Invoke(this, ChannelState.Signalling);
            }

            // Accept offer
            LoggingService.LogInfo("Accepting offer");
            var remoteSessionDescription = new RTCSessionDescriptionInit()
            {
                sdp = offer.SDP,
                type = Enum.Parse<RTCSdpType>(offer.Type.ToLower())
            };

            // Assert peer connection
            if (null == _PeerConnection)
            {
                throw new InvalidOperationException("Peer connection not initialized");
            }

            _PeerConnection.setRemoteDescription(remoteSessionDescription);

            // Create answer
            var answer = _PeerConnection.createAnswer();
            await _PeerConnection.setLocalDescription(answer);

            // Something went wrong?
            if (State != ChannelState.Signalling)
            {
                // TODO: Revert?
                LoggingService.LogWarning("Something went wrong while creating an answer");
                return;
            }

            // Send answer
            SendAnswer(new SDPInfo
            {
                SDP = answer.sdp,
                Type = answer.type.ToString()
            });
        }

        /// <summary>
        /// Rejects an SDP offer
        /// </summary>
        /// <param name="offer">The SDP offer to reject</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        public Task RejectAsync(SDPInfo offer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Closes the channel
        /// </summary>
        public Task CloseAsync()
        { 
            return CloseAsync(true);
        }

        /// <summary>
        /// Closes the channel and notifies the other party if requested
        /// 
        /// TODO: Return result or throw
        /// </summary>
        private async Task CloseAsync(bool notify)
        {
            bool shouldNotifyChange;
            lock (_Lock)
            {
                if (_State != ChannelState.Open)
                {
                    LoggingService.LogWarning("Channel is not open");
                    return;
                }

                SetState(
                    ChannelState.Closing,
                    out shouldNotifyChange,
                    out bool shouldNotifyOpen);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnStateChange?.Invoke(this, ChannelState.Closing);
            }

            try
            {
                RTCDataChannel? commandChannel;
                RTCDataChannel? dataChannel;

                lock (_Lock)
                {
                    commandChannel = _CommandChannel;
                    dataChannel = _DataChannel;
                }

                // Send close command 
                if (notify && commandChannel != null && commandChannel.readyState == RTCDataChannelState.open)
                {
                    commandChannel.send(ChannelCommand.Close.ToString());

                    // Wait for the buffered amount to be zero or a short timeout
                    const int maxWaitTime = 500; // Maximum wait time in milliseconds
                    const int checkInterval = 50; // Check every 50ms
                    int elapsedTime = 0;

                    while (commandChannel.bufferedAmount > 0 && elapsedTime < maxWaitTime)
                    {
                        await Task.Delay(checkInterval); // TODO: From settings
                        elapsedTime += checkInterval;
                    }
                }

                // Close data channel
                if (dataChannel != null)
                {
                    dataChannel.close();
                }
            }
            finally
            {
                lock (_Lock)
                {
                    // Free unmanaged resources
                    _DataChannel = null;
                }

                // Mark closed
                State = ChannelState.Closed;

                // Log
                LoggingService.LogInfo("Channel is closed");
            }
        }

        /// <summary>
        /// Sends a message over the data channel
        /// </summary>
        /// <param name="message">The message to send</param>
        public virtual void Send(string message)
        {
            if (State != ChannelState.Open || null == _DataChannel)
            {
                throw new Exception("Channel not open");
            }

            // Transmit over data channel
            _DataChannel.send(message);

            // Log message
            LoggingService.Log($">: {message}");
        }

        /// <summary>
        /// Check if the connection is stable
        /// </summary>
        /// <returns></returns>
        private bool CheckStable()
        {
            if (null == _CommandChannel)
            {
                IsStable = false;
                return false;
            }

            else if (_CommandChannel.readyState != RTCDataChannelState.open)
            {
                IsStable = false;
                return false;
            }

            if (null == _PeerConnection || _PeerConnection.iceConnectionState != RTCIceConnectionState.connected)
            {
                IsStable = false;
                return false;
            }

            IsStable = true;
            return true;
        }

        /// <summary>
        /// Modifies the stable value of the channel (should be called within a lock)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="shouldNotifyChange"></param>
        private void SetStable(bool value, out bool shouldNotifyChange)
        {
            shouldNotifyChange = false;

            // Unchanged
            if (_IsStable == value)
            {
                return;
            }

            _IsStable = value;

            // Notify outside the lock
            shouldNotifyChange = value;
        }

        /// <summary>
        /// Modifies the state of the channel (should be called within a lock)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="shouldNotifyChange"></param>
        /// <param name="shouldNotifyOpen"></param>
        private void SetState(ChannelState value, out bool shouldNotifyChange, out bool shouldNotifyOpen)
        {
            shouldNotifyChange = false;
            shouldNotifyOpen = false;

            // Unchanged
            if (_State == value)
            {
                return;
            }

            _State = value;

            // Notify outside the lock
            shouldNotifyChange = true;
            if (value == ChannelState.Open)
            {
                shouldNotifyOpen = true;
            }
        }

        /// <summary>
        /// Sets the data channel
        /// </summary>
        /// <param name="channel"></param>
        private void SetDataChannel(RTCDataChannel channel)
        {
            if (null != _DataChannel)
            {
                LoggingService.LogError("Data channel already set");
                return;
            }

            _DataChannel = channel;
            _DataChannel.onopen += OnDataChannelOpen;
            _DataChannel.onclose += OnDataChannelClose;
            _DataChannel.onerror += OnDataChannelError;
            _DataChannel.onmessage += OnDataChannelMessage;

            // Is stable and open?
            if (CheckStable() && _DataChannel.readyState == RTCDataChannelState.open)
            {
                bool shouldNotifyChange, shouldNotifyOpen;
                lock (_Lock)
                {
                    if (_State == ChannelState.Closing ||
                        _State == ChannelState.Disposing ||
                        _State == ChannelState.Disposed)
                    {
                        return;
                    }

                    SetState(
                        ChannelState.Open,
                        out shouldNotifyChange,
                        out shouldNotifyOpen);
                }

                // Notify change
                if (shouldNotifyChange)
                {
                    OnStateChange?.Invoke(this, ChannelState.Open);
                }

                // Notify open
                if (shouldNotifyOpen)
                {
                    OnOpen?.Invoke(this, new EventArgs());
                }
            }
        }

        /// <summary>
        /// Sets the command channel
        /// </summary>
        /// <param name="channel"></param>
        private void SetCommandChannel(RTCDataChannel channel)
        {
            if (null != _CommandChannel)
            {
                LoggingService.LogError("Command channel already set");
                return;
            }

            _CommandChannel = channel;
            _CommandChannel.onopen += OnCommandChannelOpen;
            _CommandChannel.onclose += OnCommandChannelClose;
            _CommandChannel.onerror += OnCommandChannelError;
            _CommandChannel.onmessage += OnCommandChannelMessage;

            // Is stable?
            CheckStable();
        }

        /// <summary>
        /// Adds an ICE candidate to the channel
        /// </summary>
        /// <param name="candidate"></param>
        protected void AddIceCandidate(IceCandidate candidate)
        {
            if (null == _PeerConnection)
            {
                LoggingService.LogError("Peer connection not initizlized");
                return;
            }

            if (candidate == null || string.IsNullOrEmpty(candidate.Candidate))
            {
                LoggingService.LogError("Invalid ICE candidate");
                return;
            }

            // Convert "0" to null for sdpMid if required
            var sdpMid = candidate.SdpMid == "0" ? null : candidate.SdpMid;

            // Add ICE Candidate
            LoggingService.LogInfo("Adding ICE candidate");
            var iceCandidate = new RTCIceCandidateInit
            {
                candidate = candidate.Candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = (ushort)(candidate.SdpMLineIndex ?? 0)
            };

            _PeerConnection.addIceCandidate(iceCandidate);
        }

        /// <summary>
        /// Disposes the channel
        /// </summary>
        public void Dispose()
        {
            Dispose(true).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the channel
        /// </summary>
        /// <param name="shouldDispose"></param>
        /// <returns></returns>
        protected virtual async Task Dispose(bool shouldDispose)
        {
            bool shouldNotifyChange;
            lock (_Lock)
            {
                if (_State == ChannelState.Disposing ||
                    _State == ChannelState.Disposed)
                {
                    return;
                }

                SetState(
                    ChannelState.Disposing,
                    out shouldNotifyChange,
                    out bool shouldNotifyOpen);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnStateChange?.Invoke(this, ChannelState.Disposing);
            }

            try
            {
                if (shouldDispose)
                {
                    // Dispose managed resources
                    RTCDataChannel? commandChannel;
                    RTCDataChannel? dataChannel;
                    RTCPeerConnection? peerConnection;

                    lock (_Lock)
                    {
                        commandChannel = _CommandChannel;
                        dataChannel = _DataChannel;
                        peerConnection = _PeerConnection;
                    }

                    if (commandChannel != null)
                    {
                        if (commandChannel.readyState == RTCDataChannelState.open)
                        {
                            // Send dispose command
                            commandChannel.send(ChannelCommand.Dispose.ToString());

                            // Wait for the buffered amount to be zero or a short timeout
                            const int maxWaitTime = 500; // Maximum wait time in milliseconds
                            const int checkInterval = 50; // Check every 50ms
                            int elapsedTime = 0;

                            while (commandChannel.bufferedAmount > 0 && elapsedTime < maxWaitTime)
                            {
                                await Task.Delay(checkInterval);
                                elapsedTime += checkInterval;
                            }
                        }

                        commandChannel.close();
                    }

                    // Close data channel
                    if (dataChannel != null)
                    {
                        dataChannel.close();
                    }

                    // Close and dispose peer connection
                    if (peerConnection != null)
                    {
                        peerConnection.close();
                        peerConnection.Dispose();
                    }
                }
            }
            finally
            {
                lock (_Lock)
                {
                    // Free unmanaged resources
                    _DataChannel = null;
                    _CommandChannel = null;
                    _PeerConnection = null;
                }

                // Mark disposed
                State = ChannelState.Disposed;

                // Notify
                OnDispose?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Destructor to catch cases where Dispose was not called
        /// </summary>
        ~BaseChannel()
        {
            Dispose(false).GetAwaiter().GetResult();
        }
        #endregion
        #region "Event Handlers"

        /// <summary>
        /// Called when a new ICE candidate is available
        /// </summary>
        /// <param name="candidate"></param>
        protected virtual void OnOnIceCandidate(RTCIceCandidate candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.candidate))
            {
                LoggingService.LogError("Invalid ICE candidate");
                return;
            }

            // Ensure sdpMid is "0" for compliance with standards
            string sdpMid = candidate.sdpMid ?? "0";

            SendCandidate(new IceCandidate
            {
                Candidate = candidate.candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = candidate.sdpMLineIndex
            });
        }

        /// <summary>
        /// Called when the ICE connection state changes
        /// </summary>
        /// <param name="state"></param>
        protected virtual void OnIceConnectionChange(RTCIceConnectionState state)
        {
            if (state != RTCIceConnectionState.connected)
            {
                return;
            }

            CheckStable();
        }

        /// <summary>
        /// Called when the connection is stable
        /// </summary>
        protected virtual void OnStable()
        {
            if (State == ChannelState.Open)
            {
                return;
            }

            LoggingService.LogInfo("Connection is stable");
            State = ChannelState.Open;

            // Close signalling
            if (SignallingService.IsOpen)
            {
                SignallingService.Disconnect();
            }
        }

        /// <summary>
        /// Called when the command channel is open
        /// </summary>
        protected virtual void OnCommandChannelOpen()
        {
            LoggingService.LogInfo("Command channel opened");
            CheckStable();
        }

        /// <summary>
        /// Called when the command channel is closed
        /// </summary>
        protected virtual void OnCommandChannelClose()
        {
            LoggingService.LogInfo("Command channel closed"); 
        }

        /// <summary>
        /// Called when an error occurs on the command channel
        /// </summary>
        /// <param name="error"></param>
        protected virtual void OnCommandChannelError(string error)
        {
            LoggingService.LogError(error);
            State = ChannelState.Failed;
        }

        /// <summary>
        /// Called when a message is received on the command channel
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="protocol"></param>
        /// <param name="bytes"></param>
        protected virtual void OnCommandChannelMessage(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] bytes)
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            if (!message.TryParseEnum<ChannelCommand>(out var command))
            {
                LoggingService.LogWarning($"Received unknown command: {message}");
                return;
            }

            // Log command
            LoggingService.Log($"/{command}");

            // Execute command
            switch (command)
            {
                case ChannelCommand.Close:
                    Task.Run(() => CloseAsync(false));
                    break;

                case ChannelCommand.Dispose:
                    Dispose();
                    break;
            }
        }

        /// <summary>
        /// Called when the data channel is open
        /// </summary>
        protected virtual void OnDataChannelOpen()
        {
            LoggingService.LogInfo("Data channel opened");
            CheckStable();
        }

        /// <summary>
        /// Called when the data channel is closed
        /// </summary>
        protected virtual void OnDataChannelClose()
        {
            LoggingService.LogInfo("Data channel closed");
        }

        /// <summary>
        /// Called when an error occurs on the data channel
        /// </summary>
        /// <param name="error"></param>
        protected virtual void OnDataChannelError(string error)
        {
            LoggingService.LogError(error);
            State = ChannelState.Failed;
        }

        /// <summary>
        /// Called when a message is received on the data channel
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="protocol"></param>
        /// <param name="bytes"></param>
        protected virtual void OnDataChannelMessage(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] bytes)
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            LoggingService.Log($"<: {message}");

            // Check if ping
            if (message.Equals("ping", StringComparison.InvariantCultureIgnoreCase))
            {
                Send("pong 2");
            }

            // Echo message
            else if (message.StartsWith("echo:", StringComparison.InvariantCultureIgnoreCase))
            { 
                Send(message.Substring("echo:".Length).TrimStart());
            }
        }

        #endregion
        #region "Abstract methods"

        protected abstract void SendAnswer(SDPInfo answer);

        protected abstract void SendRejection();

        protected abstract void SendCandidate(IceCandidate candidate);

        protected abstract void OnReceiveRejection();

        protected abstract void OnReceiveCandidate(IceCandidate candidate);
        #endregion
    }
}
