using Cryptopia.Node.RTC;
using Spectre.Console;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Displays a list of all active channels
    /// </summary>
    public class ListAccountsCommand : ICommand
    {
        /// <summary>
        /// Skip amount of accounts
        /// </summary>
        private readonly int _skip;

        /// <summary>
        /// Display amount of accounts
        /// </summary>
        private readonly int _take;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        public ListAccountsCommand(int skip, int take)
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
            var total = ChannelManager.Instance.GetAccountChannelCount();
            var channels = ChannelManager.Instance.GetAccountChannels();

            AnsiConsole.MarkupLine($"[bold yellow]{total} accounts(s) connected[/]");

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
    }
}
