using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Cryptopia.Node.Client.RPC
{
    public class RpcClient
    {
        private TcpClient _client;
        private JsonRpc _rpc;

        public RpcClient(string server, int port)
        {
            _client = new TcpClient(server, port);
        }

        public async Task ConnectAsync()
        {
            await _client.ConnectAsync("127.0.0.1", 5000);
            _rpc = JsonRpc.Attach(_client.GetStream());
        }

        public async Task<string> SendCommandAsync(string command)
        {
            switch (command.ToLower())
            {
                case "version":
                    return await _rpc.InvokeAsync<string>("GetVersion");
                case "status":
                    return await _rpc.InvokeAsync<string>("GetStatus");
                case "exit":
                    await _rpc.InvokeAsync("Exit");
                    return "Node exited.";
                default:
                    return "Unknown command.";
            }
        }

        public void Close()
        {
            _rpc?.Dispose();
            _client?.Dispose();
        }
    }
}
