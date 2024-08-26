using Cryptopia.Node.RTC;
using Spectre.Console;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Displays a list of all active node channels
    /// </summary>
    public class ListNodesCommand : ICommand
    {
        /// <summary>
        /// Skip amount of nodes
        /// </summary>
        private readonly int _skip;

        /// <summary>
        /// Display amount of nodes
        /// </summary>
        private readonly int _take;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        public ListNodesCommand(int skip, int take)
        {
            _skip = skip;
            _take = take;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            var total = ChannelManager.Instance.GetNodeChannelCount();
            var channels = ChannelManager.Instance.GetNodeChannels();

            AnsiConsole.MarkupLine($"[bold yellow]{total} nodes(s) connected[/]");

            var table = new Table();
            table.AddColumn(new TableColumn("Node").NoWrap().Width(48));
            table.AddColumn(new TableColumn("Subnet").NoWrap().Width(48));
            table.AddColumn(new TableColumn("State").NoWrap().Width(10));
            table.AddColumn(new TableColumn("Stable").NoWrap().Width(10));
            table.AddColumn(new TableColumn("Polite").NoWrap().Width(10));
            table.AddColumn(new TableColumn("Latency").NoWrap().Width(15));

            foreach (var item in channels)
            {
                var channel = item.Value;
                var state = channel.State.ToString();
                var isStable = channel.IsStable ? "[green]Yes[/]" : "[red]No[/]";
                var isPolite = channel.IsPolite ? "[green]Yes[/]" : "[red]No[/]";
                var latencyColor = channel.Latency > channel.MaxLatency ? "red" : "green";
                table.AddRow(channel.DestinationSigner.Address, "None", state, isStable, isPolite, $"[{latencyColor}]{channel.Latency} ms[/]");
            }

            AnsiConsole.Write(table);

            return 0;
        }
    }
}
