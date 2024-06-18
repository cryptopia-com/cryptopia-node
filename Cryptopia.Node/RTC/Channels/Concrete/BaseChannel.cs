using SIPSorcery.Net;

namespace Cryptopia.Node.RTC
{
    /// <summary>
    /// Represents a communication channel in the mesh network
    /// </summary>
    public abstract class BaseChannel : IChannel
    {
        /// <summary>
        /// Gets the current state of the channel
        /// </summary>
        public ChannelState State
        {
            get
            {
                return _State;
            }
            protected set
            {
                if (_State != value)
                {
                    _State = value;

                    // Notify change
                    OnStateChange?.Invoke(this, value);

                    // Notify open
                    if (value == ChannelState.Open)
                    {
                        OnOpen?.Invoke(this, new EventArgs());
                    }
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

        /// <summary>
        /// Occurs when the channel is opened
        /// </summary>
        public event EventHandler? OnOpen;

        /// <summary>
        /// Occurs when the state of the channel changes
        /// </summary>
        public event EventHandler<ChannelState>? OnStateChange;

        // Services
        protected ILoggingService LoggingService { get; private set; }
        protected ISignallingService SignallingService { get; private set; }

        // Internal
        private RTCDataChannel? _DataChannel;
        private RTCPeerConnection _PeerConnection;

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
            _PeerConnection.ondatachannel += (dataChannel) =>
            {
                if (null != _DataChannel)
                {
                    LoggingService.LogError("Data channel already set");
                    return;
                }

                _DataChannel = dataChannel;
                _DataChannel.onopen += OnDataChannelOpen;
                _DataChannel.onmessage += OnDataChannelMessage;
                _DataChannel.onclose += OnDataChannelClose;
                _DataChannel.onerror += OnDataChannelError;

                if (_DataChannel.readyState == RTCDataChannelState.open)
                {
                    CheckConnectionStable();
                }
            };
        }

        #region "Public Methods"

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

            await ConnectAsync();

            // Indicate signalling started
            State = ChannelState.Signalling;

            // Accept offer
            LoggingService.LogInfo("Accepting offer");
            var remoteSessionDescription = new RTCSessionDescriptionInit()
            {
                sdp = offer.SDP,
                type = Enum.Parse<RTCSdpType>(offer.Type.ToLower())
            };

            _PeerConnection.setRemoteDescription(remoteSessionDescription);

            var answer = _PeerConnection.createAnswer();
            await _PeerConnection.setLocalDescription(answer);

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
        /// <returns>Task that represents the asynchronous operation</returns>
        public Task RejectAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Closes the channel
        /// </summary>
        public void Close()
        {
            if (State != ChannelState.Open || null == _DataChannel)
            {
                LoggingService.LogWarning("Channel is not open");
                return;
            }

            // Send close command
            _DataChannel.send("close");

            // Close data channel
            _DataChannel.close();
            _DataChannel = null;

            // Mark closed
            State = ChannelState.Closed;

            // Log
            LoggingService.LogInfo("Connection is closed");
        }

        /// <summary>
        /// Sends a message over the channel
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>Task that represents the asynchronous operation</returns>
        public void Send(string message)
        {
            if (State != ChannelState.Open || null == _DataChannel)
            {
                throw new Exception("Channel not open");
            }

            _DataChannel.send(message);
            LoggingService.Log($">: {message}");
        }
        #endregion
        #region "Internal Methods"

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
                var timeout = DateTime.Now + TimeSpan.FromSeconds(10);
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

        private void CheckConnectionStable()
        {
            if (null == _DataChannel)
            {
                return;
            }

            if (_DataChannel.readyState != RTCDataChannelState.open)
            {
                return;
            }

            if (_PeerConnection.iceConnectionState != RTCIceConnectionState.connected)
            {
                return;
            }

            OnConnectionStable();
        }
        #endregion
        #region "Event Handlers"

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

        protected virtual void OnIceConnectionChange(RTCIceConnectionState state)
        {
            if (state != RTCIceConnectionState.connected)
            {
                return;
            }

            CheckConnectionStable();
        }

        protected virtual void OnConnectionStable()
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

        protected virtual void OnDataChannelOpen()
        {
            CheckConnectionStable();
        }

        protected virtual void OnDataChannelClose()
        {
            LoggingService.LogInfo("OnDataChannelClose");
        }

        protected virtual void OnDataChannelError(string error)
        {
            LoggingService.LogError(error);
            State = ChannelState.Failed;
        }

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

            // Close message
            else if (message.Equals("close", StringComparison.InvariantCultureIgnoreCase))
            {
                Close();
            }
        }

        #endregion
        #region "Abstract methods"

        public abstract string GetLabel();

        protected abstract void SendAnswer(SDPInfo answer);

        protected abstract void SendRejection();

        protected abstract void SendCandidate(IceCandidate candidate);

        protected abstract void OnReceiveRejection();

        protected abstract void OnReceiveCandidate(IceCandidate candidate);
        #endregion
    }
}
