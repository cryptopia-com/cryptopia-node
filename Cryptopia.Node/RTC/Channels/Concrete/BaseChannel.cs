using Cryptopia.Node.RTC.Channels.Concrete.Audit;
using Cryptopia.Node.RTC.Channels.Config.ICE;
using Cryptopia.Node.RTC.Channels.Types;
using Cryptopia.Node.RTC.Extensions;
using Cryptopia.Node.RTC.Messages;
using Cryptopia.Node.RTC.Messages.Payloads;
using Cryptopia.Node.RTC.Signalling;
using Cryptopia.Node.Services.Logging;
using SIPSorcery.Net;
using System.Timers;

namespace Cryptopia.Node.RTC.Channels.Concrete
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public abstract class BaseChannel<T> where T : BaseChannel<T>, IChannel
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
                lock (_ChannelLock)
                {
                    return _IsStable;
                }
            }
            private set
            {
                var shouldNotify = false;
                lock (_ChannelLock)
                {
                    SetStable(value,
                        out shouldNotify);
                }

                if (shouldNotify)
                {
                    OnStableDetected();
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
                lock (_ChannelLock)
                {
                    return _State;
                }
            }
            protected set
            {
                bool shouldNotifyChane, shouldNotifyOpen;
                lock (_ChannelLock)
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
        public double HeartbeatInterval { get; private set; } = 1000;

        /// <summary>
        /// Heartbeat timeout in milliseconds
        /// 
        /// The maximum timeout that the channel will tolerate before self-terminating
        /// </summary>
        public double HeartbeatTimeout { get; private set; } = 1000;

        /// <summary>
        /// Audit interval in milliseconds
        /// 
        /// The interval at which the channel will perform audits to ensure the connection is stable
        /// </summary>
        public double AuditInterval { get; private set; } = 200;

        /// <summary>
        /// Signalling timeout in milliseconds 
        /// 
        /// After this duration, the channel will consider the connection as timed out
        /// </summary>
        public double SignallingTimeout { get; set; } = 10000;

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
                lock (_HeartbeatLock)
                {
                    return _Latency;
                }
            }
            set
            {
                bool shouldNotifyChange, shouldNotifyHighLatency;
                lock (_HeartbeatLock)
                {
                    if (!_IsHeartbeatPending)
                    {
                        return;
                    }

                    _IsHeartbeatPending = false;

                    // Calculate latency
                    var nextLatency = (DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds;
                    SetLatency(
                       nextLatency,
                       out shouldNotifyChange,
                       out shouldNotifyHighLatency);
                }

                // Notify change
                if (shouldNotifyChange)
                {
                    OnLatency?.Invoke(this, _Latency);
                }

                // Notify high latency
                if (shouldNotifyHighLatency)
                {
                    OnHighLatencyDetected(_Latency);
                }
            }
        }
        private double _Latency;

        /// <summary>
        /// Indicates whether the channel has high latency (thread-safe)
        /// </summary>
        public bool IsHigLatency
        {
            get
            {
                lock (_HeartbeatLock)
                {
                    return _IsHighLatency;
                }
            }
        }
        private bool _IsHighLatency;

        // Services
        protected ILoggingService? LoggingService { get; private set; }
        protected ISignallingService SignallingService { get; private set; }

        // Internal
        private readonly object _ChannelLock = new object();
        private RTCDataChannel? _DataChannel;
        private RTCDataChannel? _CommandChannel;
        private RTCPeerConnection? _PeerConnection;
        private CancellableDelay _SignallingTimer;

        // Heartbeat
        private readonly object _HeartbeatLock = new object();
        private System.Timers.Timer? _HeartbeatTimer;
        private DateTime _LastHeartbeatTime;
        private bool _IsHeartbeatPending;
        private bool _IsHeartbeatTimeout;

        // Auditor
        private readonly object _AuditLock = new object();
        private Task<bool>? _AuditTask;
        private CancellationTokenSource? _AuditTokenSource;
        private ChannelBufferAuditor? _CommandChannelAuditor;
        private ChannelBufferAuditor? _DataChannelAuditor;

        // Constants
        private const string DATA_CHANNEL_LABEL = "data";
        private const string COMMAND_CHANNEL_LABEL = "command";

        // Events
        public event EventHandler? OnOpen;
        public event EventHandler? OnStable;
        public event EventHandler<ChannelState>? OnStateChange;
        public event EventHandler<RTCMessageEnvelope>? OnMessage;
        public event EventHandler<double>? OnLatency;
        public event EventHandler<double>? OnHighLatency;
        public event EventHandler? OnTimeout;
        public event EventHandler? OnDispose;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="isPolite"></param>
        /// <param name="isInitiatedByUs"></param>
        /// <param name="signallingService"></param>
        /// <param name="loggingService"></param>
        public BaseChannel(
            bool isPolite,
            bool isInitiatedByUs,
            ILoggingService? loggingService,
            ISignallingService signallingService)
        {
            IsPolite = isPolite;
            IsInitiatedByUs = isInitiatedByUs;
            LoggingService = loggingService;
            SignallingService = signallingService;

            // Setup signalling timer
            _SignallingTimer = new CancellableDelay(
                TimeSpan.FromMilliseconds(SignallingTimeout));
            _SignallingTimer.OnTimeout += OnSignallingTimeoutDetected;
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

            lock (_ChannelLock)
            {
                if (null != _PeerConnection)
                {
                    var exception = new InvalidOperationException("Peer connection already initialized");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
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
                    LoggingService?.LogError(
                        "Received a data channel with no label", GatherChannelData());
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
                        LoggingService?.LogError(
                            $"Received an unknown channel: {channel.label}", GatherChannelData());
                        break;
                }
            };

            return (T)this;
        }

        /// <summary>
        /// Starts the heartbeat
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="timeout"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public T StartHeartbeat(double? interval = null, double? timeout = null)
        {
            if (null == _PeerConnection)
            {
                var exception = new InvalidOperationException("Peer connection not initialized");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            lock (_HeartbeatLock)
            {
                if (null != _HeartbeatTimer)
                {
                    var exception = new InvalidOperationException("Heartbeat already started");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
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
            bool shouldNotifyChange, shouldNotifyHighLatency;
            lock (_HeartbeatLock)
            {
                if (null != _HeartbeatTimer)
                {
                    _HeartbeatTimer.Stop();
                    _HeartbeatTimer.Elapsed -= OnHeartbeatTimerElapsed;
                    _HeartbeatTimer.Dispose();
                    _HeartbeatTimer = null;
                }

                // Stopped
                _IsHeartbeatPending = false;

                SetLatency(
                   0, // No latency data available
                   out shouldNotifyChange,
                   out shouldNotifyHighLatency);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnLatency?.Invoke(this, _Latency);
            }

            // Notify high latency
            if (shouldNotifyHighLatency)
            {
                OnHighLatencyDetected(_Latency);
            }
        }

        /// <summary>
        /// Starts the auditor process
        /// </summary>
        public T StartAuditor()
        {
            if (null == _PeerConnection)
            {
                var exception = new InvalidOperationException("Peer connection not initialized");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            lock (_AuditLock)
            {
                if (null != _AuditTokenSource)
                {
                    var exception = new InvalidOperationException("Auditor already started");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
                }

                // Start auditor 
                _AuditTokenSource = new CancellationTokenSource();
                _CommandChannelAuditor = new ChannelBufferAuditor();
                _DataChannelAuditor = new ChannelBufferAuditor();
            }

            _ = Task.Run(() => RunAuditorAsync(
                _AuditTokenSource.Token));

            return (T)this;
        }

        /// <summary>
        /// Stops the auditor process
        /// </summary>
        public void StopAuditor()
        {
            lock (_AuditLock)
            {
                _AuditTokenSource?.Cancel();
                _AuditTokenSource = null;
                _CommandChannelAuditor = null;
                _DataChannelAuditor = null;
            }
        }

        /// <summary>
        /// Runs the auditor loop
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task RunAuditorAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var auditResult = await 
                     PerformAuditAsync()
                    .ConfigureAwait(false);

                if (!auditResult)
                {
                    StopAuditor(); 
                }

                await Task
                    .Delay((int)AuditInterval)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs an audit on the channel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private Task<bool> PerformAuditAsync()
        {
            Task<bool> task = null;
            lock (_AuditLock)
            {
                if (null != _AuditTask && !_AuditTask.IsCompleted)
                {
                    task = _AuditTask;
                }
            }

            if (null == task)
            {
                task = DoPerformAuditAsync();
                lock (_AuditLock)
                {
                    _AuditTask = task;
                }
            }

            return task;
        }

        /// <summary>
        /// Performs an audit on the channel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async Task<bool> DoPerformAuditAsync()
        {
            bool shouldClose = false;
            bool shouldDispose = false;

            RTCDataChannel? commandChannel = null;
            RTCDataChannel? dataChannel = null;
            ChannelBufferAuditor? commandChannelAuditor = null;
            ChannelBufferAuditor? dataChannelAuditor = null;
            ChannelState state;

            lock (_ChannelLock) 
            {
                if (IsDisposedOrDisposing())
                {
                    return false;
                }

                // Capture state
                commandChannel = _CommandChannel;
                commandChannelAuditor = _CommandChannelAuditor;
                dataChannel = _DataChannel;
                dataChannelAuditor = _DataChannelAuditor;
                state = _State;
            }

            // Audit command channel state
            if (state != ChannelState.Disposing && state != ChannelState.Disposed &&
               (null == commandChannel || commandChannel.readyState != RTCDataChannelState.open))
            {
                shouldDispose = true;

                // Log failed audit
                var data = GatherChannelData();
                data.Add("readyState", null == commandChannel 
                    ? "null" : commandChannel.readyState.ToString());
                LoggingService?.LogWarning("State Audit Failed: Command channel", data);
            }

            // Audit data channel state
            else if (state == ChannelState.Open &&
                (null == dataChannel || dataChannel.readyState != RTCDataChannelState.open))
            {
                shouldClose = true;

                // Log failed audit
                var data = GatherChannelData();
                data.Add("readyState", null == dataChannel
                    ? "null" : dataChannel.readyState.ToString());
                LoggingService?.LogWarning("State Audit Failed: Data channel", data);
            }

            // Audit command channel buffer
            else if (null != commandChannel && null != commandChannelAuditor)
            {
                try
                {
                    // Audit buffered data
                    var bufferedAmount = commandChannel.bufferedAmount;
                    shouldDispose = !commandChannelAuditor.Audit(bufferedAmount);

                    // Log failed audit
                    if (shouldDispose)
                    {
                        var data = GatherChannelData();
                        data.Add("", commandChannelAuditor.MaxBufferTime.ToString());
                        data.Add("bufferedAmount", bufferedAmount.ToString());
                        LoggingService?.LogWarning("Buffer Audit Failed: Command channel", data);
                    }
                }
                catch (Exception ex)
                {
                    shouldDispose = true;
                    LoggingService?.LogException(
                        ex, GatherChannelData());
                }
            }

            // Audit data channel buffer
            else if (null != dataChannel && null != dataChannelAuditor)
            {
                try
                {
                    // Audit buffered data
                    var bufferedAmount = dataChannel.bufferedAmount;
                    shouldClose = !dataChannelAuditor.Audit(bufferedAmount);

                    // Log failed audit
                    if (shouldClose)
                    {
                        var data = GatherChannelData();
                        data.Add("", dataChannelAuditor.MaxBufferTime.ToString());
                        data.Add("bufferedAmount", bufferedAmount.ToString());
                        LoggingService?.LogWarning("Buffer Audit Failed: Data channel", data);
                    }
                }
                catch (Exception ex)
                {
                    shouldClose = true;
                    LoggingService?.LogException(
                        ex, GatherChannelData());
                }
            }

            // Should have been disposed so dispose
            if (shouldDispose)
            {
                await DisposeAsync(true)
                     .ConfigureAwait(false);
            }

            // Should have been closed so close
            else if (shouldClose)
            {
                try
                {
                    await CloseAsync(false)
                         .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LoggingService?.LogException(
                        ex, GatherChannelData());
                }
            }

            return !shouldDispose && !shouldClose;
        }

        /// <summary>
        /// Starts the signalling timer (thread-safe)
        /// 
        /// Throws an exception if the signalling timer is already started
        /// </summary>
        private void StartSignallingTimer()
        {
            bool signallingTimerAlreadyStarted = false;
            lock (_ChannelLock)
            {
                signallingTimerAlreadyStarted = _SignallingTimer.IsStarted || _SignallingTimer.IsCancelled;
                if (!signallingTimerAlreadyStarted)
                {
                    // Start signalling timer
                    _SignallingTimer.Start();
                }
            }

            if (signallingTimerAlreadyStarted)
            {
                var exception = new InvalidOperationException("Signalling timer already started");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }
        }

        /// <summary>
        /// Connects to the signalling server
        /// </summary>
        /// <returns></returns>
        private async Task ConnectAsync()
        {
            // Indicate connecting started
            State = ChannelState.Connecting;

            // Connect to signalling server
            if (!SignallingService.IsOpen)
            {
                SignallingService.Connect();

                // Set the timeout duration for connecting (half of signalling time)
                var timeout = DateTime.Now + TimeSpan.FromMilliseconds(SignallingTimeout * 0.5f);
                while (!SignallingService.IsOpen && DateTime.Now < timeout)
                {
                    await Task
                        .Delay(100)
                        .ConfigureAwait(false);
                }

                if (!SignallingService.IsOpen)
                {
                    bool shouldNotifyChange;
                    bool shouldNotifyTimeout = false;
                    lock (_ChannelLock)
                    {
                        if (IsDisposedOrDisposing())
                        {
                            return;
                        }

                        if (_State != ChannelState.Connecting)
                        {
                            var exception = new InvalidOperationException(
                                $"Expected {ChannelState.Connecting} state instead of {_State} state");
                            LoggingService?.LogException(exception, GatherChannelData());
                            throw exception;
                        }

                        SetState(
                            ChannelState.Failed,
                            out shouldNotifyChange,
                            out bool shouldNotifyOpen);

                        if (_SignallingTimer.IsStarted && !_SignallingTimer.IsExpired)
                        {
                            _SignallingTimer.Cancel(true); // Silent because we're in a lock
                            shouldNotifyTimeout = true;
                        }
                    }

                    // Notify change
                    if (shouldNotifyChange)
                    {
                        OnStateChange?.Invoke(this, ChannelState.Failed);
                    }

                    // Notify timeout
                    if (shouldNotifyTimeout)
                    {
                        OnTimeout?.Invoke(this, new EventArgs());
                    }
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
            var state_ = State;
            if (state_ != ChannelState.Initiating)
            {
                if (state_ == ChannelState.Open)
                {
                    LoggingService?.LogWarning(
                        "Channel already open", GatherChannelData());
                    return;
                }

                else if (state_ == ChannelState.Rejected)
                {
                    if (IsPolite)
                    {
                        LoggingService?.LogWarning(
                            "Connection was rejected", GatherChannelData());
                        return;
                    }

                    // Allow
                    LoggingService?.LogInfo(
                        "Opening a rejected connection", GatherChannelData());
                }

                else if (state_ == ChannelState.Closed)
                {
                    // Allow
                    LoggingService?.LogInfo(
                        "Opening a closed connection", GatherChannelData());

                    if (_PeerConnection != null && 
                        _PeerConnection.connectionState == RTCPeerConnectionState.connected &&
                        _PeerConnection.iceConnectionState == RTCIceConnectionState.connected)
                    {
                        // TODO: Start signalling timer?

                        // Create data channel
                        var peerConnection = await _PeerConnection.createDataChannel(
                            DATA_CHANNEL_LABEL);
                        SetDataChannel(peerConnection);

                        // Wait 
                        while (State != ChannelState.Connecting && State != ChannelState.Signalling)
                        {
                            await Task
                                .Delay(100)
                                .ConfigureAwait(false);
                        }

                        return;
                    }

                    else
                    {
                        var loggingData = GatherChannelData();
                        if (null != _PeerConnection)
                        {
                            loggingData.Add("Connection state", _PeerConnection.connectionState.ToString());
                            loggingData.Add("ICE connection state", _PeerConnection.iceConnectionState.ToString());
                        }

                        LoggingService?.LogWarning("Connection not stable", loggingData);
                    }
                }

                else if (state_ == ChannelState.Failed)
                {
                    // Allow 
                    LoggingService?.LogInfo(
                        "Opening a failed connection", GatherChannelData());
                }

                else if (state_ == ChannelState.Connecting || state_ == ChannelState.Signalling)
                {
                    // Wait 
                    while (State == ChannelState.Connecting || State == ChannelState.Signalling)
                    {
                        await Task
                            .Delay(100)
                            .ConfigureAwait(false);
                    }

                    return;
                }
            }

            // Detect signalling timeout
            StartSignallingTimer();

            // Connect to signalling server
            await ConnectAsync();

            // Indicate signalling started
            bool shouldNotifyChange;
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State != ChannelState.Connecting)
                {
                    var exception = new InvalidOperationException($"Expected {ChannelState.Connecting} state instead of {_State} state");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
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

            // Create and send offer
            await OfferAsync()
                 .ConfigureAwait(false);
        }

        /// <summary>
        /// Creates and sends an SDP offer
        /// 
        /// Initiates the WebRTC handshake by creating and sending an offer to the remote peer
        /// </summary>
        /// <returns></returns>
        private async Task OfferAsync()
        {
            // Enforce signalling state
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State != ChannelState.Signalling)
                {
                    var exception = new InvalidOperationException("Offer can only be created in the signalling state");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
                }
            }

            if (null == _PeerConnection)
            {
                var exception = new InvalidOperationException("Peer connection not initialized");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            // Create channels
            SetDataChannel(await _PeerConnection.createDataChannel(DATA_CHANNEL_LABEL));
            SetCommandChannel(await _PeerConnection.createDataChannel(COMMAND_CHANNEL_LABEL));

            // Create offer
            var offer = _PeerConnection.createOffer();
            await _PeerConnection.setLocalDescription(offer);

            // Assert signalling state
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State != ChannelState.Signalling)
                {
                    var exception = new InvalidOperationException($"Expected {ChannelState.Signalling} state instead of {_State} state");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
                }
            }

            // Send offer
            SendOffer(new SDPInfo()
            {
                Type = offer.type.ToString(),
                SDP = offer.sdp
            });
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
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            if (State != ChannelState.Initiating)
            {
                var exception = new InvalidOperationException("Offer can only be accepted in the initiating state");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            // Detect signalling timeout
            StartSignallingTimer();

            // Connect to signalling server
            await ConnectAsync()
                 .ConfigureAwait(false);

            // Indicate signalling started
            bool shouldNotifyChange;
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State != ChannelState.Connecting)
                {
                    var exception = new InvalidOperationException($"Expected {ChannelState.Connecting} state instead of {_State} state");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
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
            var remoteSessionDescription = new RTCSessionDescriptionInit()
            {
                sdp = offer.SDP,
                type = Enum.Parse<RTCSdpType>(offer.Type.ToLower())
            };

            // Assert peer connection
            if (null == _PeerConnection)
            {
                var exception = new InvalidOperationException("Peer connection not initialized");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            _PeerConnection.setRemoteDescription(remoteSessionDescription);

            // Create answer
            var answer = _PeerConnection.createAnswer();
            await _PeerConnection.setLocalDescription(answer);

            // Assert signalling state
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State != ChannelState.Signalling)
                {
                    var exception = new InvalidOperationException($"Expected {ChannelState.Signalling} state instead of {_State} state");
                    LoggingService?.LogException(exception, GatherChannelData());
                    throw exception;
                }
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
            lock (_ChannelLock)
            {
                if (_State != ChannelState.Open)
                {
                    LoggingService?.LogWarning(
                        "Channel is not open", GatherChannelData());
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

                lock (_ChannelLock)
                {
                    commandChannel = _CommandChannel;
                    dataChannel = _DataChannel;
                }

                // Send close command 
                if (notify && commandChannel != null && commandChannel.readyState == RTCDataChannelState.open)
                {
                    SendCommand(ChannelCommand.Close);

                    // Wait for the buffered amount to be zero or a short timeout
                    const int maxWaitTime = 500; // Maximum wait time in milliseconds
                    const int checkInterval = 50; // Check every 50ms
                    int elapsedTime = 0;

                    while (commandChannel.bufferedAmount > 0 && elapsedTime < maxWaitTime)
                    {
                        await Task.Delay(checkInterval).ConfigureAwait(false); // TODO: From settings
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
                lock (_ChannelLock)
                {
                    // Free unmanaged resources
                    _DataChannel = null;
                }

                // Mark closed
                State = ChannelState.Closed;
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
                var exception = new InvalidOperationException("Channel not open");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            // Transmit over data channel
            _DataChannel.send(message);
        }

        /// <summary>
        /// Send a command over the command channel
        /// </summary>
        /// <param name="command"></param>
        /// <param name="audit"></param>
        private void SendCommand(ChannelCommand command, bool audit = true)
        {
            if (null == _CommandChannel)
            {
                var exception = new InvalidOperationException("Channel not open");
                LoggingService?.LogException(exception, GatherChannelData());
                throw exception;
            }

            _CommandChannel.send(command.ToString());

            // Record bufferred data
            if (audit)
            {
                _CommandChannelAuditor?.Record(
                    (uint)command.ToString().Length);
            }
        }

        /// <summary>
        /// Sends a heartbeat message to the remote peer
        /// 
        /// Sends a ping message to the remote peer over the command channel in ordert to calculate the 
        /// round-trip latency (RTT) and detect whether the connection is still alive or not
        /// </summary>
        private void SendHeartbeat()
        {
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
                _IsHeartbeatTimeout = false;
            }

            // Ping
            SendCommand(ChannelCommand.Ping);
        }

        /// <summary>
        /// Sends a pong message to the remote peer over the command channel
        /// as a response to a ping message
        /// </summary>
        private void SendHeartbeatResponse()
        {
            // Pong
            SendCommand(ChannelCommand.Pong);
        }

        /// <summary>
        /// Receives a pong message from the remote peer over the command channel
        /// as a response to a ping message
        /// </summary>
        private void ReceiveHeartbeatResponse()
        {
            bool shouldNotifyChange, shouldNotifyHighLatency;
            lock (_HeartbeatLock)
            {
                if (!_IsHeartbeatPending)
                {
                    return;
                }

                _IsHeartbeatPending = false;

                // Calculate latency
                var nextLatency = (DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds;
                SetLatency(
                   nextLatency,
                   out shouldNotifyChange,
                   out shouldNotifyHighLatency);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnLatency?.Invoke(this, _Latency);
            }

            // Notify high latency
            if (shouldNotifyHighLatency)
            {
                OnHighLatencyDetected(_Latency);
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
        /// Sets the latency (not thread-safe)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="shouldNotifyChange"></param>
        /// <param name="shouldNotifyHighLatency"></param>

        private void SetLatency(double value, out bool shouldNotifyChange, out bool shouldNotifyHighLatency)
        {
            shouldNotifyChange = false;
            shouldNotifyHighLatency = false;

            // Unchanged
            if (_Latency == value)
            {
                return;
            }

            _Latency = value;

            // Notify outside the lock
            shouldNotifyChange = true;
            if (_Latency > MaxLatency)
            {
                if (!_IsHighLatency)
                {
                    _IsHighLatency = true;
                    shouldNotifyHighLatency = true;
                }
            }

            else
            {
                _IsHighLatency = false;
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
                LoggingService?.LogError(
                    "Data channel already set", GatherChannelData());
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
                lock (_ChannelLock)
                {
                    if (IsDisposedOrDisposing())
                    {
                        return;
                    }

                    if (_State == ChannelState.Closing)
                    {
                        return;
                    }

                    // Cancel the signalling timer
                    if (_SignallingTimer.IsStarted && !_SignallingTimer.IsExpired && !_SignallingTimer.IsCancelled)
                    {
                        _SignallingTimer.Cancel(true); // Silent because we're in a lock
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
                LoggingService?.LogError(
                    "Command channel already set", GatherChannelData());
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
        /// Sets the SDP answer
        /// 
        /// Processes the received SDP answer and updates the local peer connection
        /// </summary>
        /// <param name="answer">The SDP answer to set</param>
        /// <returns></returns>
        protected void SetAnswer(SDPInfo awnser)
        {
            if (State != ChannelState.Signalling)
            {
                LoggingService?.LogError(
                    "Can only set answer in the signalling state", 
                    GatherChannelData());
                return;
            }

            if (null == _PeerConnection)
            {
                LoggingService?.LogError(
                    "Peer connection not initizlized", GatherChannelData());
                return;
            }

            var remoteSessionDescription = new RTCSessionDescriptionInit()
            {
                type = awnser.Type.ToEnum<RTCSdpType>(),
                sdp = awnser.SDP
            };

            _PeerConnection.setRemoteDescription(remoteSessionDescription);
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
                LoggingService?.LogError(
                    "Peer connection not initizlized", GatherChannelData());
                return;
            }

            if (candidate == null || string.IsNullOrEmpty(candidate.Candidate))
            {
                LoggingService?.LogError(
                    "Invalid ICE candidate", GatherChannelData());
                return;
            }

            // Convert "0" to null for sdpMid if required
            var sdpMid = candidate.SdpMid == "0" ? null : candidate.SdpMid;

            // Add ICE Candidate
            var iceCandidate = new RTCIceCandidateInit
            {
                candidate = candidate.Candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = (ushort)(candidate.SdpMLineIndex ?? 0)
            };

            _PeerConnection.addIceCandidate(iceCandidate);
        }

        /// <summary>
        /// Returns whether the channel is disposed or disposing (not thread-safe)
        /// </summary>
        /// <returns></returns>
        public bool IsDisposedOrDisposing(bool aquireLock = false)
        {
            if (aquireLock)
            {
                lock (_ChannelLock)
                {
                    return IsDisposedOrDisposing(false);
                }
            }

            return _State == ChannelState.Disposing || _State == ChannelState.Disposed;
        }

        /// <summary>
        /// Disposes the channel and frees resources
        /// 
        /// Handles the cleanup of resources when the channel is no longer needed and notifies the other party
        /// </summary>
        public void Dispose()
        {
            DisposeAsync(true).GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the channel and optionally disposes managed resources
        /// 
        /// Handles the cleanup of resources when the channel is no longer needed and notifies the other party
        /// </summary>
        /// <param name="shouldDispose">Indicates whether managed resources should be disposed</param>
        /// <returns></returns>
        protected virtual async Task DisposeAsync(bool shouldDispose)
        {
            bool shouldNotifyChange;
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
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

                    lock (_ChannelLock)
                    {
                        commandChannel = _CommandChannel;
                        dataChannel = _DataChannel;
                        peerConnection = _PeerConnection;
                    }

                    // Stop monitoring
                    StopAuditor();
                    StopHeartbeat();

                    // Dispose managed resources
                    if (commandChannel != null)
                    {
                        if (commandChannel.readyState == RTCDataChannelState.open)
                        {
                            // Send dispose command
                            SendCommand(ChannelCommand.Dispose, false);

                            // Wait for the buffered amount to be zero or a short timeout
                            const int maxWaitTime = 500; // Maximum wait time in milliseconds
                            const int checkInterval = 50; // Check every 50ms
                            int elapsedTime = 0;

                            while (commandChannel.bufferedAmount > 0 && elapsedTime < maxWaitTime)
                            {
                                await Task
                                    .Delay(checkInterval)
                                    .ConfigureAwait(false);

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
                lock (_ChannelLock)
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
            DisposeAsync(false).GetAwaiter().GetResult();
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
                LoggingService?.LogError(
                    "Invalid ICE candidate", GatherChannelData());
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
        protected virtual void OnStableDetected()
        {
            bool shouldNotifyChange, shouldNotifyOpen;
            lock (_ChannelLock)
            {
                if (IsDisposedOrDisposing())
                {
                    return;
                }

                if (_State == ChannelState.Closing ||
                    _State == ChannelState.Failed ||
                    _State == ChannelState.Open)
                {
                    return;
                }

                // Cancel the signalling timer
                if (_SignallingTimer.IsStarted && !_SignallingTimer.IsExpired && !_SignallingTimer.IsCancelled)
                {
                    _SignallingTimer.Cancel(true); // Silent because we're in a lock
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

            // Notify
            OnStable?.Invoke(this, EventArgs.Empty);

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
            CheckStable();
        }

        /// <summary>
        /// Event handler for command channel close event
        /// 
        /// Called when the command channel is closed
        /// </summary>
        protected virtual void OnCommandChannelClose()
        {
            // Nothing here
        }

        /// <summary>
        /// Event handler for command channel errors
        /// 
        /// Handles errors that occur on the command channel and updates the channel state accordingly
        /// </summary>
        /// <param name="error">The error that occurred</param>
        protected virtual void OnCommandChannelError(string error)
        {
            LoggingService?.LogError(error, GatherChannelData());
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
                LoggingService?.LogWarning(
                    $"Received unknown command: {message}", GatherChannelData());
                return;
            }

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
                    _ = Task.Run(() => CloseAsync(false));
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
            CheckStable();
        }

        /// <summary>
        /// Event handler for data channel close event
        /// 
        /// Called when the data channel is closed
        /// </summary>
        protected virtual void OnDataChannelClose()
        {
            // Nothing here
        }

        /// <summary>
        /// Event handler for data channel errors
        /// 
        /// Handles errors that occur on the data channel and updates the channel state accordingly
        /// </summary>
        /// <param name="error">The error that occurred</param>
        protected virtual void OnDataChannelError(string error)
        {
            LoggingService?.LogError(error, GatherChannelData());
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
                LoggingService?.Log($">: pong", GatherChannelData());
                Send("pong");
            }

            // Echo message
            else if (message.StartsWith("echo:", StringComparison.InvariantCultureIgnoreCase))
            {
                LoggingService?.Log($">: {message.Substring("echo:".Length).TrimStart()}", GatherChannelData());
                Send(message);
            }

            // Parse message
            else if (message.TryDeserializeRTCMessage(out var envelope))
            {
                // Assert
                if (null == envelope)
                {
                    LoggingService?.LogError(
                        "Failed to deserialize message", GatherChannelData());
                    return;
                }

                // Notify child class
                OnReceiveMessage(envelope);

                // Notify subscribers
                OnMessage?.Invoke(this, envelope);
            }
            else
            {
                LoggingService?.LogError(
                    "Failed to deserialize message", GatherChannelData());
            }
        }

        /// <summary>
        /// Called when the signalling timer was allowed to exprire
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSignallingTimeoutDetected(object? sender, EventArgs e)
        {
            // Mark as failed
            bool shouldNotifyChange = false;
            lock (_ChannelLock)
            {
                // Set by other thread?
                if (_State != ChannelState.Connecting &&
                    _State != ChannelState.Signalling)
                {
                    return;
                }

                SetState(
                    ChannelState.Failed,
                    out shouldNotifyChange,
                    out bool shouldNotifyOpen);
            }

            // Notify change
            if (shouldNotifyChange)
            {
                OnStateChange?.Invoke(this, ChannelState.Failed);
            }

            // Notify timeout
            OnTimeout?.Invoke(this, new EventArgs());
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
                    if (!_IsHeartbeatTimeout && (DateTime.UtcNow - _LastHeartbeatTime).TotalMilliseconds > HeartbeatTimeout)
                    {
                        _IsHeartbeatTimeout = true;
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
                OnHeartbeatTimeoutDetected();
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
        protected virtual void OnHeartbeatTimeoutDetected()
        {
            OnTimeout?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when high latency is detected
        /// </summary>
        /// <param name="latency"></param>
        protected virtual void OnHighLatencyDetected(double latency)
        {
            OnHighLatency?.Invoke(this, latency);
        }

        #endregion
        #region "Abstract methods"

        /// <summary>
        /// Gathers logging data for the channel
        /// </summary>
        /// <returns></returns>
        protected abstract IDictionary<string, string> GatherChannelData();

        /// <summary>
        /// Sends an SDP offer
        /// 
        /// Transmits an SDP offer to the remote peer to initiate the WebRTC handshake
        /// </summary>
        /// <param name="offer">The SDP offer to send</param>
        /// <returns></returns>
        protected abstract void SendOffer(SDPInfo offer);

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
        /// Handles reception of an SDP answer
        /// 
        /// Processes the received SDP answer and updates the local peer connection
        /// </summary>
        /// <param name="answer">The SDP answer received</param>
        protected abstract void OnReceiveAnswer(SDPInfo answer);

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
