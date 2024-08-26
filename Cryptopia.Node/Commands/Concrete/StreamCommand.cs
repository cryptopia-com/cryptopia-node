using static Program;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Enables streaming mode
    /// </summary>
    public class StreamCommand : ICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            Program.SetMode(Mode.Stream);
            return 0;
        }
    }
}
