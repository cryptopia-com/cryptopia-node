﻿using SIPSorcery.Net;
using System.Timers;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public abstract class BaseChannel<T>  where T : BaseChannel<T>, IChannel
    {
        /// <summary>
        /// Indicates whether the channel is stable (thread-safe)
        /// 
        /// Stability in this context means that the command channel is open, and the ICE connection 
        /// state is connected. Meaning that an RTC connection between the peers has been established
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
        /// Represents the current state of the channel (thread-safe)
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
        /// Indicates whether the channel is polite
        /// 
        /// A polite channel waits for the remote peer to initiate certain actions like ICE restarts or 
        /// re-offers. This behavior helps in scenarios where both peers might try to handle the situation 
        /// simultaneously, leading to conflicts
        /// </summary>
        public bool IsPolite { get; private set; }

        /// <summary>
        /// Indicates whether the channel was initiated by us
        /// 
        /// This property helps in identifying the origin of the channel initiation, which can be useful 
        /// for handling signaling and negotiation logic correctly
        /// </summary>
        public bool IsInitiatedByUs { get; private set; }

        /// <summary>
        /// Heartbeat interval in milliseconds
        /// 
        /// The interval at which the channel will send ping messages to the 
        /// remote peer in order to ensure the connection is still alive
        /// </summary>
        public double HeartbeatInterval { get; private set; } = 5000;

        /// <summary>
        /// Heartbeat timeout in milliseconds
        /// 
        /// The maximum timeout that the channel will tolerate before self-terminating
        /// </summary>
        public double HeartbeatTimeout { get; private set; } = 1000;

        /// <summary>
        /// Max latency in milliseconds (zero means no latency)
        /// 
        /// Anything above this value will be considered high latency 
        /// </summary>
        public double MaxLatency { get; set; } = 200;

        /// <summary>
        /// Max latency in milliseconds (zero means no latency data available)
        /// </summary>
        public double Latency
        {
            get
            {
                return _Latency;
            }
            set
            {
                if (_Latency != value)
                {
                    _Latency = value;

                    // Notify
                    OnLatency?.Invoke(this, value);
                }
            }
        }
        private double _Latency;

        // Services
        protected ILoggingService LoggingService { get; private set; }
        protected ISignallingService SignallingService { get; private set; }

        // Internal
        private readonly object _Lock = new object();
        private RTCDataChannel? _DataChannel;
        private RTCDataChannel? _CommandChannel;
        private RTCPeerConnection? _PeerConnection;

        // Heartbeat
        private readonly object _HeartbeatLock = new object();
        private System.Timers.Timer? _HeartbeatTimer;
        private DateTime _LastHeartbeatTime;
        private bool _IsHeartbeatPending;
        private bool _isHeartbeatTimeout;

        // Constants
        private const string DATA_CHANNEL_LABEL = "data";
        private const string COMMAND_CHANNEL_LABEL = "command";

        // Events
        public event EventHandler? OnOpen;
        public event EventHandler<ChannelState>? OnStateChange;
        public event EventHandler<RTCMessageEnvelope>? OnMessage;
        public event EventHandler<double>? OnLatency;
        public event EventHandler? OnHighLatency;
        public event EventHandler? OnTimeout;
        public event EventHandler? OnDispose;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="isPolite"></param>
        /// <param name="isInitiatedByUs"></param>
        /// <param name="loggingService"></param>
        /// <param name="signallingService"></param>
        public BaseChannel(
            bool isPolite, 
            bool isInitiatedByUs, 
            ILoggingService loggingService, 
            ISignallingService signallingService)
        {
            IsPolite = isPolite;
            IsInitiatedByUs = isInitiatedByUs;
            LoggingService = loggingService;
            SignallingService = signallingService;
        }

        #region "Methods"

        /// <summary>
        /// Starts the peer connection with the specified ICE servers
        /// </summary>
        /// <param name="iceServers"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public T StartPeerConnection(IICEServerConfig[]? iceServers = null)
        {
            // Configure peer connection
            var peerConfig = new RTCConfiguration();
            var iceServerList = new List<RTCIceServer>();
            if (null != iceServers)
            {
                for (int i = 0; i < iceServers.Length; i++)
                {
                    iceServerList.Add(new RTCIceServer()
                    {
                        urls = iceServers[i].Urls,
                    });
                }
            }
            peerConfig.iceServers = iceServerList;

            lock (_Lock)
            {
                if (null != _PeerConnection)
                {
                    throw new InvalidOperationException("Peer connection already initialized");
                }

                // Create peer connection
                _PeerConnection = new RTCPeerConnection(peerConfig);
            }
            
            // Set up event handlers
            _PeerConnection.onicecandidate += OnOnIceCandidate;
            _PeerConnection.oniceconnectionstatechange += OnIceConnectionChange;
            _PeerConnection.ondatachannel += (channel) =>
            {
                if (channel == null || string.IsNullOrEmpty(channel.label))
                {
                    LoggingService.LogError("Received a data channel with no label");
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
                        LoggingService.LogError($"Received an unknown channel: {channel.label}");
                        break;
                }
            };

            return (T)this;
        }

        /// <summary>
        /// Starts the heartbeat
        /// </summary>
        /// <param name="interval"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public T StartHeartbeat(double? interval = null, double? timeout = null)
        {
            lock (_HeartbeatLock)
            {
                if (null != _HeartbeatTimer)
                {
                    throw new InvalidOperationException("Heartbeat already started");
                }

                // Start heartbeat 
                if (interval.HasValue)
                {
                    HeartbeatInterval = interval.Value;
                }

                if (timeout.HasValue)
                {
                    HeartbeatTimeout = timeout.Value;
                }

                _HeartbeatTimer = new System.Timers.Timer(
                    Math.Min(HeartbeatInterval, HeartbeatTimeout));
                _HeartbeatTimer.Elapsed += OnHeartbeatTimerElapsed;
                _HeartbeatTimer.Start();
            }

            return (T)this;
        }

        /// <summary>
        /// Stops the heartbeat
        /// </summary>
        public void StopHeartbeat()
        {
            lock (_HeartbeatLock)
            {
                if (null != _HeartbeatTimer)
                {
                    _HeartbeatTimer.Stop();
                    _HeartbeatTimer.Elapsed -= OnHeartbeatTimerElapsed;
                    _HeartbeatTimer.Dispose();
                    _HeartbeatTimer = null;
                }
                
                HeartbeatInterval = 0; // Stopped  
            }

            Latency = 0; // No latency data available
        }

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

        /// <summary>
        /// (Re)opens the channel and establishes the RTC connection (thread-safe)
        /// 
        /// 1) Ensures a connection to the signalling server
        /// 2) Creates an offer and sends it to the remote peer
        /// </summary>
        /// <returns></returns>
        public async Task OpenAsync()
        {
            await ConnectAsync();
        }

        /// <summary>
        /// Accepts an SDP offer to establish the channel
        /// 
        /// Processes a received offer and generates an answer to complete the handshake 
        /// </summary>
        /// <param name="offer"></param>
        /// <returns></returns>
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
        /// remote peer
        /// </summary>
        /// <param name="offer">The SDP offer to reject</param>
        /// <returns></returns>
        public Task RejectAsync(SDPInfo offer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Closes the channel
        /// 
        /// Handles the graceful shutdown of the channel by closing the data channel. The command channel 
        /// remains open to allow the reopening of the channel if needed
        /// </summary>
        /// <returns></returns>
        public Task CloseAsync()
        { 
            return CloseAsync(true);
        }

        /// <summary>
        /// Closes the channel and optionally notifies the other party
        /// 
        /// Handles the graceful shutdown of the channel by closing the data channel. The command channel 
        /// remains open to allow the reopening of the channel if needed
        /// </summary>
        /// <param name="notify">Indicates whether to notify the other party of the closure</param>
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
        }

        /// <summary>
        /// Sends a heartbeat message to the remote peer
        /// 
        /// Sends a ping message to the remote peer over the command channel in ordert to calculate the 
        /// round-trip latency (RTT) and detect whether the connection is still alive or not
        /// </summary>
        private void SendHeartbeat()
        {
            if (null == _CommandChannel)
            {
                throw new Exception("Channel not open");
            }

            // Reset 
            lock (_HeartbeatLock)
            {
                // Stopped?
                if (null == _HeartbeatTimer)
                {
                    return;
                }

                _LastHeartbeatTime = DateTime.UtcNow;
                _IsHeartbeatPending = true;
                _isHeartbeatTimeout = false;
            }

            // Ping
            _CommandChannel?.send(ChannelCommand.Ping.ToString());
        }

        /// <summary>
        /// Sends a pong message to the remote peer over the command channel
        /// as a response to a ping message
        /// </summary>
        private void SendHeartbeatResponse()
        {
            if (null == _CommandChannel)
            {
                throw new Exception("Channel not open");
            }

            // Pong
            _CommandChannel?.send(ChannelCommand.Pong.ToString());
        }

        /// <summary>
        /// Receives a pong message from the remote peer over the command channel
        /// as a response to a ping message
        /// </summary>
        private void ReceiveHeartbeatResponse()
        {
            lock (_HeartbeatLock)
            {
                if (!_IsHeartbeatPending)
                {
                    return;
                }

                _IsHeartbeatPending = false;
            }

            // Calculate latency
            Latency = (DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds;

            // High latency?
            if (Latency > MaxLatency)
            {
                OnHighLatencyDetected();
            }
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
        /// Sets the stability status of the channel (not thread-safe)
        /// </summary>
        /// <param name="value">The new stability status</param>
        /// <param name="shouldNotifyChange">Indicates whether a notification should be triggered</param>
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
        /// Sets the state of the channel (not thread-safe)
        /// </summary>
        /// <param name="value">The new channel state</param>
        /// <param name="shouldNotifyChange">Indicates whether a notification should be triggered</param>
        /// <param name="shouldNotifyOpen">Indicates whether an open notification should be triggered</param>
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
        /// 
        /// Assigns the data channel and sets up its event handlers
        /// </summary>
        /// <param name="channel">The data channel to set</param>
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
        /// 
        /// Assigns the command channel and sets up its event handlers
        /// </summary>
        /// <param name="channel">The command channel to set</param>
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
        /// Adds an ICE candidate to the peer connection
        /// 
        /// Handles the reception and addition of ICE candidates necessary for establishing the WebRTC connection
        /// </summary>
        /// <param name="candidate">The ICE candidate to add</param>
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
        /// Disposes the channel and frees resources
        /// 
        /// Handles the cleanup of resources when the channel is no longer needed and notifies the other party
        /// </summary>
        public void Dispose()
        {
            Dispose(true).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the channel and optionally disposes managed resources
        /// 
        /// Handles the cleanup of resources when the channel is no longer needed and notifies the other party
        /// </summary>
        /// <param name="shouldDispose">Indicates whether managed resources should be disposed</param>
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

                    // Stop heartbeat
                    StopHeartbeat();

                    // Dispose managed resources
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
        /// Event handler for ICE candidate reception
        /// 
        /// Processes incoming ICE candidates and adds them to the peer connection
        /// </summary>
        /// <param name="candidate">The ICE candidate received</param>
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
        /// Event handler for ICE connection state change
        /// 
        /// Handles changes in the ICE connection state and updates the channel state accordingly
        /// </summary>
        /// <param name="state">The new ICE connection state</param>
        protected virtual void OnIceConnectionChange(RTCIceConnectionState state)
        {
            if (state != RTCIceConnectionState.connected)
            {
                return;
            }

            CheckStable();
        }

        /// <summary>
        /// Handles actions when the connection becomes stable
        /// 
        /// Stability indicates that the command channel is open and the ICE connection is established
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
        /// Event handler for command channel open event
        /// 
        /// Called when the command channel is successfully opened
        /// </summary>
        protected virtual void OnCommandChannelOpen()
        {
            LoggingService.LogInfo("Command channel opened");
            CheckStable();
        }

        /// <summary>
        /// Event handler for command channel close event
        /// 
        /// Called when the command channel is closed
        /// </summary>
        protected virtual void OnCommandChannelClose()
        {
            LoggingService.LogInfo("Command channel closed"); 
        }

        /// <summary>
        /// Event handler for command channel errors
        /// 
        /// Handles errors that occur on the command channel and updates the channel state accordingly
        /// </summary>
        /// <param name="error">The error that occurred</param>
        protected virtual void OnCommandChannelError(string error)
        {
            LoggingService.LogError(error);
            State = ChannelState.Failed;
        }

        /// <summary>
        /// Event handler for messages received on the command channel
        /// 
        /// Processes incoming messages on the command channel
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
                case ChannelCommand.Ping:
                    SendHeartbeatResponse(); // Send pong
                    break;

                case ChannelCommand.Pong:
                    ReceiveHeartbeatResponse(); // Receive pong
                    break;

                case ChannelCommand.Close:
                    Task.Run(() => CloseAsync(false));
                    break;

                case ChannelCommand.Dispose:
                    Dispose();
                    break;
            }
        }

        /// <summary>
        /// Event handler for data channel open event
        /// 
        /// Called when the data channel is successfully opened
        /// </summary>
        protected virtual void OnDataChannelOpen()
        {
            LoggingService.LogInfo("Data channel opened");
            CheckStable();
        }

        /// <summary>
        /// Event handler for data channel close event
        /// 
        /// Called when the data channel is closed
        /// </summary>
        protected virtual void OnDataChannelClose()
        {
            LoggingService.LogInfo("Data channel closed");
        }

        /// <summary>
        /// Event handler for data channel errors
        /// 
        /// Handles errors that occur on the data channel and updates the channel state accordingly
        /// </summary>
        /// <param name="error">The error that occurred</param>
        protected virtual void OnDataChannelError(string error)
        {
            LoggingService.LogError(error);
            State = ChannelState.Failed;
        }

        /// <summary>
        /// Event handler for messages received on the data channel
        /// 
        /// Processes incoming messages on the data channel
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="protocol"></param>
        /// <param name="bytes"></param>
        protected virtual void OnDataChannelMessage(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] bytes)
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);

            // Check if ping
            if (message.Equals("ping", StringComparison.InvariantCultureIgnoreCase))
            {
                LoggingService.Log($">: pong");
                Send("pong");
            }

            // Echo message
            else if (message.StartsWith("echo:", StringComparison.InvariantCultureIgnoreCase))
            {
                LoggingService.Log($">: {message.Substring("echo:".Length).TrimStart()}");
                Send(message);
            }

            // Parse message
            else if (message.TryDeserializeRTCMessage(out var envelope))
            {
                // Assert
                if (null == envelope)
                {
                    LoggingService.LogError("Failed to deserialize message");
                    return;
                }

                // Notify child class
                OnReceiveMessage(envelope);

                // Notify subscribers
                OnMessage?.Invoke(this, envelope);
            }
            else 
            {
                LoggingService.LogError("Failed to deserialize message");
            }
        }

        /// <summary>
        /// Called when the heartbeat timer elapses
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnHeartbeatTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsStable)
            {
                return;
            }

            var notifyHeartBeatTimeout = false;
            var sendHeartbeat = false;

            lock (_HeartbeatLock)
            {
                if (_IsHeartbeatPending)
                {
                    // Heartbeat Timeout?
                    if (!_isHeartbeatTimeout && (DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds > HeartbeatTimeout)
                    {
                        _isHeartbeatTimeout = true;
                        notifyHeartBeatTimeout = true;
                    }
                }
                else
                {
                    // Heartbeat pending?
                    if ((DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds >= HeartbeatInterval)
                    {
                        sendHeartbeat = true;
                    }
                }
            }
            
            // Notify timeout
            if (notifyHeartBeatTimeout)
            {
                OnHeartbeatTimeout();
            }

            // Send heartbeat
            else if (sendHeartbeat)
            {
                SendHeartbeat();
            }
        }

        /// <summary>
        /// Called when the heartbeat times out
        /// </summary>
        private void OnHeartbeatTimeout()
        {
            LoggingService.LogError("Heartbeat timeout");
            OnTimeout?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when high latency is detected
        /// </summary>
        private void OnHighLatencyDetected()
        {
            LoggingService.LogWarning("High latency detected");
            OnHighLatency?.Invoke(this, EventArgs.Empty);
        }

        #endregion
        #region "Abstract methods"

        /// <summary>
        /// Sends an SDP answer
        /// 
        /// Transmits an SDP answer to the remote peer to complete the WebRTC handshake
        /// </summary>
        /// <param name="answer">The SDP answer to send</param>
        /// <returns></returns>
        protected abstract void SendAnswer(SDPInfo answer);

        /// <summary>
        /// Sends an SDP rejection
        /// 
        /// Transmits an SDP rejection to the remote peer, indicating that the offer was not accepted
        /// </summary>
        /// <param name="offer">The SDP offer to reject</param>
        /// <returns></returns>
        protected abstract void SendRejection(SDPInfo offer);

        /// <summary>
        /// Sends an ICE candidate
        /// 
        /// Transmits an ICE candidate to the remote peer to assist in establishing the connection
        /// </summary>
        /// <param name="candidate">The ICE candidate to send</param>
        /// <returns></returns>
        protected abstract void SendCandidate(IceCandidate candidate);

        /// <summary>
        /// Handles reception of an SDP rejection
        /// 
        /// Processes the received SDP rejection and updates the channel state accordingly
        /// </summary>
        protected abstract void OnReceiveRejection();

        /// <summary>
        /// Handles reception of an ICE candidate
        /// 
        /// Processes the received ICE candidate and adds it to the local peer connection
        /// </summary>
        /// <param name="candidate">The ICE candidate received</param>
        protected abstract void OnReceiveCandidate(IceCandidate candidate);

        /// <summary>
        /// Called when a message is received
        /// 
        /// Handles incoming messages encapsulated in an RTCMessageEnvelope
        /// </summary>
        /// <param name="envelope">The received RTC message envelope</param>
        protected abstract void OnReceiveMessage(RTCMessageEnvelope envelope);

        #endregion
    }
}
