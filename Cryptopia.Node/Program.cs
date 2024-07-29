using CommandLine;
using Cryptopia.Node.ApplicationInsights;
using Cryptopia.Node.RTC;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Spectre.Console;
using System.Reflection;
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
    public class StreamOptions { }

    [Verb("list", HelpText = "List all connected channels")]
    public class ListOptions
    {
        [Option("all", HelpText = "List all channels")]
        public bool All { get; set; }
    }

    private static TelemetryClient? _TelemetryClient;

    /// <summary>
    /// The application mode
    /// </summary>
    public enum Mode
    {
        Stream,
        Listen
    }

    /// <summary>
    /// The current mode
    /// </summary>
    public static Mode CurrentMode { get; private set; }

    // Internal
    private static Task? _StreamingTask;
    private static Task? _ListeningTask;
    private static CancellationTokenSource? _StreamingCTS;
    private static CancellationTokenSource? _ListeningCTS;

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
    }

    /// <summary>
    /// Run the application
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    private static int Run(bool stream)
    {
        // Are we using Application Insights?
        var insightsConnectionString = Environment.GetEnvironmentVariable("c");
        if (!string.IsNullOrEmpty(insightsConnectionString))
        {
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.ConnectionString = insightsConnectionString;

            var telemetryClient = new TelemetryClient(configuration);
            var insightsLoggingService = new ApplicationInsightsLoggingService(telemetryClient);
            
            // Use insights to log unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                telemetryClient.TrackException(e.ExceptionObject as Exception);
                telemetryClient.Flush();
                Thread.Sleep(100);
            };

            // Use insights logging service
            ChannelManager.Instance.LoggingService = insightsLoggingService;
        }

        // Listen for process exit
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        // Setup WebSocket server
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
        var server = new WebSocketServer($"ws://0.0.0.0:{port}");
        server.AddWebSocketService<RTCSignallingBehavior>("/");
        server.Start();

        // Set initial mode
        SetMode(stream ? Mode.Stream : Mode.Listen);

        // Cancel token on Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            // Disable streaming if it is enabled
            if (Mode.Stream == CurrentMode)
            {
                SetMode(Mode.Listen);
                e.Cancel = true;
                return;
            }

            server.Stop();
            StopListening();
            StopStreaming();
            e.Cancel = false;
        };

        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    /// <summary>
    /// Update the application mode
    /// </summary>
    /// <param name="mode"></param>
    private static void SetMode(Mode mode)
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

    /// <summary>
    /// Start streaming to the console
    /// </summary>
    private static void StartStreaming()
    {
        _StreamingCTS = new CancellationTokenSource();
        _StreamingTask = Task.Run(() => Stream(_StreamingCTS.Token));
    }

    /// <summary>
    /// Ends streaming to the console
    /// </summary>
    private static void StopStreaming()
    {
        _StreamingCTS?.Cancel();
        _StreamingTask?.Wait();
        _StreamingCTS?.Dispose();
        _StreamingCTS = null;
    }

    /// <summary>
    /// Streams to the console
    /// </summary>
    /// <param name="token"></param>
    private static void Stream(CancellationToken token)
    {
        var table = new Table();
        table.AddColumn(new TableColumn("Account").NoWrap().Width(48));
        table.AddColumn(new TableColumn("Device").NoWrap().Width(48));
        table.AddColumn(new TableColumn("State").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Stable").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Polite").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Latency").NoWrap().Width(15));

        bool isConsoleCleared = false;

        AnsiConsole.Clear();
        AnsiConsole.Live(new Rows(
                new Markup("\n"), // Adding space between the text and the table
                new Markup("\n"), // Adding space between the text and the table
                new Markup("[bold yellow]\n\nCryptopia Node[/]").Centered(),
                new Markup("\n"), // Adding space between the text and the table
                new Markup("\n"), // Adding space between the text and the table
                new Markup($"[bold yellow]0 account(s) connected[/]"),
                table
            ))
            .AutoClear(true)
            .Start(ctx =>
            {
                while (!token.IsCancellationRequested)
                {
                    // Clear the table before updating
                    table.Rows.Clear();

                    var channels = ChannelManager.Instance.GetChannels();
                    var totalAccounts = channels.Count;
                    if (totalAccounts == 0)
                    {
                        table.AddEmptyRow();
                    }
                    else
                    {
                        foreach (var accountChannels in channels)
                        {
                            var account = accountChannels.Key;
                            foreach (var channel in accountChannels.Value.Values)
                            {
                                var state = channel.State.ToString();
                                var isStable = channel.IsStable ? "[green]Yes[/]" : "[red]No[/]";
                                var isPolite = channel.IsPolite ? "[green]Yes[/]" : "[red]No[/]";
                                var latencyColor = channel.Latency > channel.MaxLatency ? "red" : "green";
                                table.AddRow(account, channel.DestinationSigner.Address, state, isStable, isPolite, $"[{latencyColor}]{channel.Latency} ms[/]");
                            }
                        }
                    }

                    ctx.UpdateTarget(new Rows(
                        new Markup("\n"), // Adding space between the text and the table
                        new Markup("\n"), // Adding space between the text and the table
                        new FigletText("Cryptopia Node").Centered().Color(Color.White),
                        new Markup("\n"), // Adding space between the text and the table
                        new Markup("\n"), // Adding space between the text and the table
                        new Markup($"[bold yellow]{totalAccounts} account(s) connected[/]"),
                        table
                    ));

                    Thread.Sleep(100);
                }

                Thread.Sleep(100); // Allow the console to clear
                isConsoleCleared = true;
            });

        while (!isConsoleCleared)
        {
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Start listening for commands
    /// </summary>
    private static void StartListening()
    {
        _ListeningCTS = new CancellationTokenSource();
        //_ListeningTask = Task.Run(() => Listen(_ListeningCTS.Token));

        Listen(_ListeningCTS.Token);
    }

    /// <summary>
    /// Stop listening for commands
    /// </summary>
    private static void StopListening()
    {
        _ListeningCTS?.Cancel();
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
            Console.Write("> ");
            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            var args = input.Split(' ');
            Parser.Default.ParseArguments<VersionOptions, StatusOptions, StreamOptions, ListOptions>(args)
                .MapResult(
                    (VersionOptions opts) => RunVersionCommand(),
                    (StatusOptions opts) => RunStatusCommand(),
                    (StreamOptions opts) => RunStreamCommand(),
                    (ListOptions opts) => RunListCommand(opts.All),
                    errs => HandleParseError(errs));
        }
    }

    /// <summary>
    /// Display the version information
    /// </summary>
    /// <returns></returns>
    private static int RunVersionCommand()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AnsiConsole.MarkupLine($"[bold yellow]Cryptopia Node Version {version}[/]");
        return 0;
    }

    /// <summary>
    /// Display the status information
    /// </summary>
    /// <returns></returns>
    private static int RunStatusCommand()
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
        var channelCount = ChannelManager.Instance.GetChannelCount();

        var table = new Table();
        table.AddColumn("Info");
        table.AddColumn("Value");

        table.AddRow("WebSocket server port", port);
        table.AddRow("Connected accounts", channelCount.ToString());

        AnsiConsole.Write(table);

        return 0;
    }

    /// <summary>
    /// Enable streaming
    /// </summary>
    /// <returns></returns>
    private static int RunStreamCommand()
    {
        SetMode(Mode.Stream);
        return 0;
    }

    /// <summary>
    /// Display all connected channels
    /// </summary>
    /// <param name="all"></param>
    /// <returns></returns>
    private static int RunListCommand(bool all)
    {
        var channels = ChannelManager.Instance.GetChannels();

        var table = new Table();
        table.AddColumn(new TableColumn("Account").NoWrap().Width(48));
        table.AddColumn(new TableColumn("Device").NoWrap().Width(48));
        table.AddColumn(new TableColumn("State").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Stable").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Polite").NoWrap().Width(10));
        table.AddColumn(new TableColumn("Latency").NoWrap().Width(15));

        foreach (var accountChannels in channels)
        {
            var account = accountChannels.Key;
            foreach (var channel in accountChannels.Value.Values)
            {
                var state = channel.State.ToString();
                var isStable = channel.IsStable ? "[green]Yes[/]" : "[red]No[/]";
                var isPolite = channel.IsPolite ? "[green]Yes[/]" : "[red]No[/]";
                var latencyColor = channel.Latency > channel.MaxLatency ? "red" : "green";
                table.AddRow(account, channel.DestinationSigner.Address, state, isStable, isPolite, $"[{latencyColor}]{channel.Latency} ms[/]");
            }
        }

        AnsiConsole.Write(table);

        return 0;
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
    /// Called when the process is exiting
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnProcessExit(object? sender, EventArgs e)
    {
        ChannelManager.Instance.Dispose();
    }
}