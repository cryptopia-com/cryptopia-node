namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Exits the Cryptopia Node
    /// </summary>
    public class ExitCommand : ICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            Program.Exit();
            return 0;
        }
    }
}
