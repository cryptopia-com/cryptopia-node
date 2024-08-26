using Spectre.Console;
using System.Reflection;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Returns the current version of the Cryptopia Node
    /// </summary>
    public class VersionCommand : ICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AnsiConsole.MarkupLine($"[bold yellow]Cryptopia Node Version {version}[/]");
            return 0;
        }
    }
}
