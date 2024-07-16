using Cryptopia.Node.RTC;
using WebSocketSharp.Server;

public class Program
{
    /// <summary>
    /// Program EntryPoint
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        // Attach the ProcessExit event to the handler method
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
        var server = new WebSocketServer($"ws://0.0.0.0:{port}");
        server.AddWebSocketService<RTCSignallingBehavior>("/");
        server.Start();

        Console.WriteLine($"WebSocket server listing on port {port}");

        // Configure the ChannelManager
        ChannelManager.Instance.ConsoleOutput = false;

        // Keep the application running
        Console.CancelKeyPress += (sender, e) => 
        { 
            server.Stop();
        };

        Thread.Sleep(Timeout.Infinite);
    }

    /// <summary>
    /// Event handler for ProcessExit event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnProcessExit(object? sender, EventArgs e)
    {
        ChannelManager.Instance.Dispose();
    }
}