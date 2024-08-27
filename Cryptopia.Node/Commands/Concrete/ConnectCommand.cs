using Cryptopia.Node.RTC;
using Cryptopia.Node.RTC.Signalling;
using Spectre.Console;

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
        private readonly string _Signer;

        /// <summary>
        /// Endpoint to connect to
        /// </summary>
        private readonly string _Endpoint;

        /// <summary>
        /// Signalling service
        /// </summary>
        private readonly ISignallingServiceFactory _SignallingFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="signer"></param>
        /// <param name="endpoint"></param>
        /// <param name="endpoint"></param>
        public ConnectCommand(string signer, string endpoint, ISignallingServiceFactory signallingFactory)
        {
            _Signer = signer;
            _Endpoint = endpoint;
            _SignallingFactory = signallingFactory;
        }

        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>Status</returns>
        public int Execute()
        {
            var endpoint = _Endpoint;
            if (!endpoint.StartsWith("ws"))
            {
                endpoint = $"ws://{_Endpoint}:8000";
            }

            // Resolve the factory from the DI container
            var signalling = _SignallingFactory.Create(endpoint);
            _ = Task.Run(() => 
                ChannelManager.Instance
                    .CreateNodeChannel(_Signer, signalling)
                    .OpenAsync());

            AnsiConsole.MarkupLine($"[bold green]OK[/]");
            return 0;
        }
    }
}
