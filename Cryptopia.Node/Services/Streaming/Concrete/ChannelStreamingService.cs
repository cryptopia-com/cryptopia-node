using Cryptopia.Node.RTC;
using Spectre.Console;

namespace Cryptopia.Node.Services.Streaming
{
    /// <summary>
    /// Streaming service for channel information
    /// </summary>
    public class ChannelStreamingService : IStreamingService
    {
        // Internal
        private Task? _streamingTask;
        private CancellationTokenSource? _streamingCTS;

        /// <summary>
        /// Starts the streaming service
        /// </summary>
        public void Start()
        {
            _streamingCTS = new CancellationTokenSource();
            _streamingTask = Task.Run(() => Stream(_streamingCTS.Token));
        }

        /// <summary>
        /// Stops the streaming service
        /// </summary>
        public void Stop()
        {
            _streamingCTS?.Cancel();
            _streamingTask?.Wait();
            _streamingCTS?.Dispose();
            _streamingCTS = null;
        }

        /// <summary>
        /// Stream channels to the console
        /// </summary>
        /// <param name="token"></param>
        private void Stream(CancellationToken token)
        {
            var nodeTable = new Table();
            nodeTable.AddColumn(new TableColumn("Node").NoWrap().Width(48));
            nodeTable.AddColumn(new TableColumn("Subnet").NoWrap().Width(48));
            nodeTable.AddColumn(new TableColumn("State").NoWrap().Width(10));
            nodeTable.AddColumn(new TableColumn("Stable").NoWrap().Width(10));
            nodeTable.AddColumn(new TableColumn("Polite").NoWrap().Width(10));
            nodeTable.AddColumn(new TableColumn("Latency").NoWrap().Width(15));

            var accountTable = new Table();
            accountTable.AddColumn(new TableColumn("Account").NoWrap().Width(48));
            accountTable.AddColumn(new TableColumn("Device").NoWrap().Width(48));
            accountTable.AddColumn(new TableColumn("State").NoWrap().Width(10));
            accountTable.AddColumn(new TableColumn("Stable").NoWrap().Width(10));
            accountTable.AddColumn(new TableColumn("Polite").NoWrap().Width(10));
            accountTable.AddColumn(new TableColumn("Latency").NoWrap().Width(15));

            AnsiConsole.Clear();
            AnsiConsole.Live(new Rows(
                    new Markup("\n"),
                    new Markup("\n"),
                    new Markup("[bold yellow]\n\nCryptopia Node[/]").Centered(),
                    new Markup("\n"),
                    new Markup("\n"),
                    new Markup($"[bold yellow]0 node(s) connected[/]"),
                    nodeTable,
                    new Markup("\n"), // Adding space between the node and account tables
                    new Markup($"[bold yellow]0 accounts(s) connected[/]"),
                    accountTable
                ))
                .AutoClear(true)
                .Start(ctx =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        // Clear the tables before updating
                        nodeTable.Rows.Clear();
                        accountTable.Rows.Clear();

                        // Fetch and display node channels
                        var nodeChannels = ChannelManager.Instance.GetNodeChannels();
                        var totalNodes = nodeChannels.Count;

                        foreach (var nodeChannel in nodeChannels.Values)
                        {
                            var state = nodeChannel.State.ToString();
                            var isStable = nodeChannel.IsStable ? "[green]Yes[/]" : "[red]No[/]";
                            var isPolite = nodeChannel.IsPolite ? "[green]Yes[/]" : "[red]No[/]";
                            var latencyColor = nodeChannel.Latency > nodeChannel.MaxLatency ? "red" : "green";
                            nodeTable.AddRow(nodeChannel.DestinationSigner.Address, "None", state, isStable, isPolite, $"[{latencyColor}]{nodeChannel.Latency} ms[/]");
                        }

                        // Fetch and display account channels
                        var accountChannels = ChannelManager.Instance.GetAccountChannels();
                        var totalAccounts = accountChannels.Count;

                        foreach (var accountChannel in accountChannels)
                        {
                            var account = accountChannel.Key;
                            foreach (var channel in accountChannel.Value.Values)
                            {
                                var state = channel.State.ToString();
                                var isStable = channel.IsStable ? "[green]Yes[/]" : "[red]No[/]";
                                var isPolite = channel.IsPolite ? "[green]Yes[/]" : "[red]No[/]";
                                var latencyColor = channel.Latency > channel.MaxLatency ? "red" : "green";
                                accountTable.AddRow(account, channel.DestinationSigner.Address, state, isStable, isPolite, $"[{latencyColor}]{channel.Latency} ms[/]");
                            }
                        }

                        // Update the tables with the current state
                        ctx.UpdateTarget(new Rows(
                            new Markup("\n"),
                            new Markup("\n"),
                            new FigletText("Cryptopia Node").Centered().Color(Color.White),
                            new Markup("\n"),
                            new Markup("\n"),
                            new Markup($"[bold yellow]{totalNodes} node(s) connected[/]"),
                            nodeTable,
                            new Markup("\n"), // Adding space between the node and account tables
                            new Markup($"[bold yellow]{totalAccounts} account(s) connected[/]"),
                            accountTable
                        ));

                        Thread.Sleep(100);  // Small delay to avoid overloading the CPU
                    }

                    Thread.Sleep(100);  // Allow the console to clear
                });
        }

        /// <summary>
        /// Dispose of the streaming service
        /// </summary>
        public void Dispose()
        {
            _streamingCTS?.Dispose();
        }
    }
}
