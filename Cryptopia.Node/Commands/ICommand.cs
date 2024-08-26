namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Generic command interface
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Result</returns>
        int Execute();
    }
}
