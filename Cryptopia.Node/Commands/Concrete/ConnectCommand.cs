using Cryptopia.Node.RTC;

namespace Cryptopia.Node.Commands
{
    /// <summary>
    /// Connects to a node
    /// </summary>
    public class ConnectCommand : ICommand
    {
        /// <summary>
        /// Signer of the node to connect to
        /// </summary>
        private readonly string _signer;

        /// <summary>
        /// Endpoint to connect to
        /// </summary>
        private readonly string _endpoint;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="signer"></param>
        /// <param name="endpoint"></param>
        public ConnectCommand(string signer, string endpoint)
        {
            _signer = signer;
            _endpoint = endpoint;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            //ChannelManager.Instance.CreateNodeChannel(_endpoint);
            return 0;
        }
    }
}
