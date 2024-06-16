using WebSocketSharp.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8000";
        var server = new WebSocketServer($"ws://0.0.0.0:{port}");
        server.AddWebSocketService<RTCSignallingBehavior>("/");
        server.Start();

        Console.WriteLine($"WebSocket server listing on port {port}");

        // Keep the application running
        Console.CancelKeyPress += (sender, e) => 
        { 
            server.Stop();
        };

        Thread.Sleep(Timeout.Infinite);
    }
}