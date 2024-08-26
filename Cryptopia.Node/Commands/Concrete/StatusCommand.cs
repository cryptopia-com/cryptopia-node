using Cryptopia.Node.RTC;
using Spectre.Console;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Returns the current status of the Cryptopia Node
    /// </summary>
    public class StatusCommand : ICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
            var nodeChannelCount = ChannelManager.Instance.GetNodeChannelCount();
            var accountChannelCount = ChannelManager.Instance.GetAccountChannelCount();

            var table = new Table();
            table.AddColumn("Info");
            table.AddColumn("Value");

            table.AddRow("WebSocket server port", port);
            table.AddRow("Connected nodes", nodeChannelCount.ToString());
            table.AddRow("Connected accounts", accountChannelCount.ToString());

            AnsiConsole.Write(table);

            return 0;
        }
    }
}
