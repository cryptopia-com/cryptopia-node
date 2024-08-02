namespace Cryptopia.Node
{
    /// <summary>
    /// CancellableDelay provides a one-time delay that can be cancelled
    /// </summary>
    public class CancellableDelay
    {
        /// <summary>
        /// Gets a value indicating whether the timer has been started
        /// </summary>
        public bool IsStarted => _DelayTask != null;

        /// <summary>
        /// Gets a value indicating whether the timer has expired
        /// </summary>
        public bool IsExpired => null != _DelayTask && _DelayTask.IsCompletedSuccessfully;

        /// <summary>
        /// Gets a value indicating whether the timer has been cancelled
        /// </summary>
        public bool IsCancelled => _CTS.Token.IsCancellationRequested;

        // Internal
        private readonly TimeSpan _Timeout;
        private readonly CancellationTokenSource _CTS;
        private Task? _DelayTask;

        // Events
        public EventHandler? OnTimeout;
        public EventHandler? OnCancellation;

        /// <summary>
        /// Initializes a new instance of the OneTimeCancellableTimer class
        /// </summary>
        /// <param name="timeout">The duration after which the timer will trigger the timeout action if not cancelled</param>
        public CancellableDelay(TimeSpan timeout)
        {
            _Timeout = timeout;
            _CTS = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the timer
        /// 
        /// If the timer runs until the end OnTimeout will be invoked
        /// </summary>
        public void Start()
        {
            _DelayTask = Task.Delay(_Timeout, _CTS.Token).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && !task.IsCanceled)
                {
                    OnTimeout?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        /// <summary>
        /// Cancels the timer if it is still running
        /// </summary>
        /// <param name="silent"></param>
        public void Cancel(bool silent = false)
        {
            _CTS.Cancel();

            if (!silent)
            {
                OnCancellation?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
