using CommandLine;
using Cryptopia.Node;
using Cryptopia.Node.Commands;
using Cryptopia.Node.RPC;
using Cryptopia.Node.RTC;
using Cryptopia.Node.Services.Logging;
using Cryptopia.Node.Services.Streaming;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Security;
using WebSocketSharp.Server;

/// <summary>
/// Cryptopia Node application
/// </summary>
public class Program
{
    [Verb("run", HelpText = "Run the Cryptopia Node application.")]
    public class RunOptions
    {
        [Option('s', "stream", Default = false, HelpText = "Enable streaming to the console")]
        public bool Stream { get; set; }
    }

    [Verb("v", HelpText = "Display the version information")]
    public class VersionOptions { }

    [Verb("status", HelpText = "Display the status information")]
    public class StatusOptions { }

    [Verb("stream", HelpText = "enable streaming")]
    public class StreamOptions 
    {
        [Option("nodes", Default = false, HelpText = "Stream nodes")]
        public bool Nodes { get; set; }

        [Option("accounts", Default = false, HelpText = "Stream accounts")]
        public bool Accounts { get; set; }
    }

    [Verb("list", HelpText = "List connected node channels")]
    public class ListOptions
    {
        [Option("nodes", Default = false, HelpText = "List nodes")]
        public bool Nodes { get; set; }

        [Option("accounts", Default = false, HelpText = "List accounts")]
        public bool Accounts { get; set; }

        [Option("skip", Default = 0, HelpText = "Amount of channels to skip")]
        public int Skip { get; set; }

        [Option("take", Default = 100, HelpText = "Amount of channels to take")]
        public int Take { get; set; }
    }

    [Verb("connect", HelpText = "Connect to a node or account")]
    public class ConnectOptions
    {
        [Option("node", Default = false, HelpText = "The endpoint of the node to connect to")]
        public string? Node { get; set; }
    }

    [Verb("exit", HelpText = "Exit the application")]
    public class ExitOptions { }

    /// <summary>
    /// The application mode
    /// </summary>
    public enum Mode
    {
        None,
        Stream,
        Listen
    }

    /// <summary>
    /// The current mode
    /// </summary>
    public static Mode CurrentMode { get; private set; }

    // Internal
    private static WebSocketServer? _Server;
    private static TelemetryClient? _TelemetryClient;
    private static Task? _ListeningTask;
    private static CancellationTokenSource? _ListeningCTS;
    private static CancellationTokenSource _ApplicationCTS = new CancellationTokenSource();
    private static IStreamingService _StreamingService = new ChannelStreamingService();

    private static string? _Input = null;
    private static bool _InputProcessing = false;
    private static bool _InputAvailable = false;
    private static readonly object _InputLock = new object();
    private static readonly object _ModeLock = new object();

    /// <summary>
    /// Entry point 
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            args = new[] { "run" };
        }
        else if (args[0].StartsWith("-"))
        {
            args = new string[] { "run" }.Concat(args).ToArray();
        }

        Parser.Default.ParseArguments<RunOptions>(args)
            .MapResult(
                (RunOptions opts) => Run(opts.Stream),
                errs => HandleParseError(errs));

        // Keep the main thread running until cancellation is requested
        _ApplicationCTS.Token.WaitHandle.WaitOne();
    }

    /// <summary>
    /// Run the application
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static int Run(bool stream)
    {
        ILoggingService loggingService;

        // Use Application Insights logging service
        var insightsConnectionString = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(insightsConnectionString))
        {
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.ConnectionString = insightsConnectionString;

            _TelemetryClient = new TelemetryClient(configuration);
            loggingService = new ApplicationInsightsLoggingService(_TelemetryClient);

            // Use insights to log unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                _TelemetryClient.TrackException(e.ExceptionObject as Exception);
                _TelemetryClient.Flush();
                Thread.Sleep(50);
            };
        }

        // Use the default logging service
        else
        {
            loggingService = new AppLoggingService();
        }

        // Listen for process exit
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        // Setup channel manager
        var privateKey = new SecureString();
        foreach (var c in Environment.GetEnvironmentVariable("PRIVATE_KEY") ?? string.Empty)
        {
            privateKey.AppendChar(c);
        }

        if (privateKey.Length == 0)
        {
            var exception = new ArgumentException("The PRIVATE_KEY environment variable is required");
            loggingService.LogException(exception);
            throw exception;
        }

        AccountManager.Instance.Signer = new LocalAccount(privateKey, 0);
        ChannelManager.Instance.LoggingService = loggingService;

        // Setup WebSocket server
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
        _Server = new WebSocketServer($"ws://0.0.0.0:{port}");
        _Server.AddWebSocketService("/", () => new SignallingBehavior(loggingService));
        _Server.Start();

        _ = Task.Run(() => ProcessInput(_ApplicationCTS.Token));

        // Set initial mode
        SetMode(stream ? Mode.Stream : Mode.Listen);

        // Cancel Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            HandleCancelKeyPress(e);
        };

        return 0;
    }

    /// <summary>
    /// Called when the cancel key is pressed
    /// </summary>
    /// <param name="e"></param>
    private static void HandleCancelKeyPress(ConsoleCancelEventArgs e)
    {
        try
        {
            // Disable streaming if it is enabled
            if (Mode.Stream == CurrentMode)
            {
                SetMode(Mode.Listen);
                e.Cancel = true;
                return;
            }

            _Server?.Stop();
            StopListening();
            StopStreaming();
            _StreamingService.Dispose();
        }
        catch (Exception ex)
        {
            _TelemetryClient?.TrackException(ex);
            throw;
        }
        finally
        {
            if (!e.Cancel)
            {
                _ApplicationCTS.Cancel();
                lock (_InputLock)
                {
                    // Notify all waiting threads to exit
                    Monitor.PulseAll(_InputLock); 
                }
            }
        }

        e.Cancel = false;
    }

    /// <summary>
    /// Process input on the main thread that is read from the console by a separate thread
    /// </summary>
    /// <param name="token"></param>
    private static void ProcessInput(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string? inputToProcess = null;

            lock (_InputLock)
            {
                // Wait for input to be available
                while (!_InputAvailable || _InputProcessing)
                {
                    Monitor.Wait(_InputLock);
                }

                if (!string.IsNullOrEmpty(_Input))
                {
                    inputToProcess = _Input;

                    // Reset the input
                    _Input = null;
                    _InputAvailable = false;

                    // Signal input processing
                    _InputProcessing = true;
                    Monitor.PulseAll(_InputLock);
                }

                else
                {
                    // Reset the input state if it's empty
                    _InputAvailable = false;
                    _InputProcessing = false;

                    // Signal nothing to process
                    Monitor.PulseAll(_InputLock);
                }
            }

            if (inputToProcess != null)
            {
                // Execute the command (outside the lock to avoid holding the lock for long periods)
                var args = inputToProcess.Split(' ');
                Parser.Default.ParseArguments<VersionOptions, StatusOptions, StreamOptions, ListOptions, ExitOptions>(args)
                    .MapResult(
                        (VersionOptions opts) => new VersionCommand().Execute(),
                        (StatusOptions opts) => new StatusCommand().Execute(),
                        (StreamOptions opts) => new StreamCommand().Execute(),
                        (ListOptions opts) => 
                        {
                            ICommand command;
                            if (opts.Nodes)
                            {
                                command = new ListNodesCommand(
                                    opts.Skip, opts.Take);
                            }

                            else 
                            { 
                                command = new ListAccountsCommand(
                                    opts.Skip, opts.Take);
                            }

                            return command.Execute();
                        },
                        (ExitOptions opts) => new ExitCommand().Execute(),
                        errs => HandleParseError(errs));

                lock (_InputLock)
                {
                    // Signal input processed
                    _InputProcessing = false;
                    Monitor.PulseAll(_InputLock);
                }
            }
        }
    }

    /// <summary>
    /// Update the application mode
    /// </summary>
    /// <param name="mode"></param>
    public static void SetMode(Mode mode)
    {
        lock (_ModeLock)
        {
            if (CurrentMode != mode) 
            {
                switch (mode)
                {
                    case Mode.Stream:
                        StopListening();
                        StartStreaming();
                        break;
                    case Mode.Listen:
                        StopStreaming();
                        StartListening();
                        break;
                }

                CurrentMode = mode;
            }
        }
    }

    /// <summary>
    /// Start streaming to the console
    /// </summary>
    private static void StartStreaming()
    {
        _StreamingService.Start();
    }

    /// <summary>
    /// Ends streaming to the console
    /// </summary>
    private static void StopStreaming()
    {
        _StreamingService.Stop();
    }

    /// <summary>
    /// Start listening for commands
    /// </summary>
    private static void StartListening()
    {
        _ListeningCTS = new CancellationTokenSource();
        _ListeningTask = Task.Run(() => Listen(_ListeningCTS.Token), _ListeningCTS.Token); 
    }

    /// <summary>
    /// Stop listening for commands
    /// </summary>
    private static void StopListening()
    {
        _ListeningCTS?.Cancel();
        lock (_InputLock)
        {
            // Signal task cancellation
            Monitor.PulseAll(_InputLock);
        }

        _ListeningTask?.Wait();
        _ListeningCTS?.Dispose();
        _ListeningCTS = null;
    }

    /// <summary>
    /// Detects the command and runs the appropriate action
    /// </summary>
    /// <param name="token"></param>
    private static void Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            lock (_InputLock)
            {
                // Wait for the input to be processed
                while ((_InputProcessing || _InputAvailable) && !token.IsCancellationRequested)
                {
                    Monitor.Wait(_InputLock);
                }

                // Check if the task was cancelled
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Console.Write("> ");
                var inputTask = Task.Run(() => Console.ReadLine(), token);

                try
                {
                    inputTask.Wait(token);
                    var input = inputTask.Result;

                    if (!string.IsNullOrEmpty(input))
                    {
                        _Input = input;
                        _InputAvailable = true;

                        // Signal input available
                        Monitor.PulseAll(_InputLock);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Handle the task cancellation gracefully
                }
                finally
                {
                    // Ensure Monitor.PulseAll is called in case of an exception
                    if (!_InputAvailable)
                    {
                        Monitor.PulseAll(_InputLock);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called when there is a parsing error
    /// </summary>
    /// <param name="errs"></param>
    /// <returns></returns>
    private static int HandleParseError(IEnumerable<Error> errs)
    {
        foreach (var error in errs)
        {
            Console.WriteLine(error.ToString());
        }

        return 1;
    }

    /// <summary>
    /// Exit the application
    /// </summary>
    /// <returns></returns>
    public static void Exit()
    {
        try
        {
            _Server?.Stop();
            StopListening();
            StopStreaming();
            _StreamingService.Dispose();
        }
        catch (Exception ex)
        {
            _TelemetryClient?.TrackException(ex);
            throw;
        }
        finally
        {
            _ApplicationCTS.Cancel();
            lock (_InputLock)
            {
                // Notify all waiting threads to exit
                Monitor.PulseAll(_InputLock);
            }
        }
    }

    /// <summary>
    /// Called when the process is exiting
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnProcessExit(object? sender, EventArgs e)
    {
        ChannelManager.Instance.Dispose();
        _TelemetryClient?.Flush();
        Thread.Sleep(1000); 
    }
}