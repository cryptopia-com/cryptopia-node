using System.Collections.Concurrent;

namespace Cryptopia.Node.RTC.Channels.Concrete.Audit
{
    /// <summary>
    /// Audits sent and buffered data to ensure the connection is still alive
    /// </summary>
    public class ChannelBufferAuditor
    {
        /// <summary>
        /// Entity for tracking sent and buffered data
        /// </summary>
        public struct BufferedData
        {
            /// <summary>
            /// 
            /// Data size that was buffered
            /// </summary>
            public ulong BufferSize { get; private set; }

            /// <summary>
            /// Expect the data to be out of the buffer by this time
            /// </summary>
            public DateTime ExpirationTimestamp { get; private set; }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="bufferSize"></param>
            /// <param name="expirationTimestamp"></param>
            public BufferedData(ulong bufferSize, DateTime expirationTimestamp)
            {
                BufferSize = bufferSize;
                ExpirationTimestamp = expirationTimestamp;
            }
        }

        /// <summary>
        /// Cleaning interval in milliseconds
        /// 
        /// The interval at which the auditor will clean the sent and buffered data
        /// </summary>
        public double CleaningInterval { get; private set; } = 50;

        /// <summary>
        /// The max time that data can be buffered for
        /// </summary>
        public double MaxBufferTime { get; private set; } = 500;

        /// <summary>
        /// Collection of sent and buffered data
        /// </summary>
        private ConcurrentQueue<BufferedData> _SentDataQueue = new ConcurrentQueue<BufferedData>();

        /// <summary>
        /// Used to cancel the cleaning process
        /// </summary>
        private CancellationTokenSource _CancellationSource = new CancellationTokenSource();

        /// <summary>
        /// Constructor
        /// </summary>
        public ChannelBufferAuditor()
        {
            _ = Task.Run(() => CleanAsync(
                _CancellationSource.Token));
        }

        /// <summary>
        /// Records the sent data
        /// </summary>
        /// <param name="bufferSize"></param>
        public void Record(ulong bufferSize)
        {
            _SentDataQueue.Enqueue(new BufferedData(
                bufferSize,
                DateTime.UtcNow + TimeSpan.FromMilliseconds(MaxBufferTime)));
        }

        /// <summary>
        /// Performs an audit on the sent and buffered data
        /// 
        /// It checks if the actual buffer size is within the limit of the maximum buffer size that 
        /// is allowed to be in the buffer currently
        /// </summary>
        /// <param name="currentBufferSize"></param>
        /// <returns></returns>
        public bool Audit(ulong currentBufferSize)
        {
            // Copy for audit
            var sentDataQueue = new Queue<BufferedData>(_SentDataQueue);

            // Check if the queue is empty
            if (0 == sentDataQueue.Count)
            {
                return true;
            }

            // Check if the oldest data has expired
            ulong maxBufferSize = 0;
            while (sentDataQueue.Count > 0)
            {
                if (sentDataQueue.TryPeek(out var oldestData))
                {
                    if (DateTime.UtcNow > oldestData.ExpirationTimestamp)
                    {
                        // Expired - Not allowed in buffer
                        sentDataQueue.TryDequeue(out _);
                    }

                    else
                    {
                        // Allowed in buffer
                        maxBufferSize += oldestData.BufferSize;
                    }
                }
            }

            // Check if the current buffer size is within the limit
            return currentBufferSize <= maxBufferSize;
        }

        /// <summary>
        /// Performs cleanup of the sent and buffered data
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CleanAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (_SentDataQueue.Count > 0)
                {
                    if (_SentDataQueue.TryPeek(out var oldestData))
                    {
                        if (DateTime.UtcNow > oldestData.ExpirationTimestamp)
                        {
                            // Expired - Not allowed in buffer
                            _SentDataQueue.TryDequeue(out _);
                        }
                    }
                }

                await Task
                    .Delay((int)CleaningInterval)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~ChannelBufferAuditor()
        {
            _CancellationSource.Cancel();
        }
    }
}
