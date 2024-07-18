using CommandLine;
using System.IO.Pipes;

public class Program
{
    [Verb("version", HelpText = "Display the version information")]
    public class VersionOptions { }

    [Verb("status", HelpText = "Display the status information")]
    public class StatusOptions { }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<VersionOptions, StatusOptions>(args)
            .MapResult(
                (VersionOptions opts) => ExecuteCommand("version"),
                (StatusOptions opts) => ExecuteCommand("status"),
                errs => HandleParseError(errs));
    }

    private static int ExecuteCommand(string command)
    {
        using (var client = new NamedPipeClientStream("CryptopiaNodePipe"))
        {
            try
            {
                client.Connect(1000); // 1 second timeout

                if (!client.IsConnected)
                {
                    Console.WriteLine("Error: Cryptopia Node is not running.");
                    return 1;
                }

                // Use the `LeaveOpen` parameter to prevent the client from being disposed by the StreamReader and StreamWriter
                using (var writer = new StreamWriter(client, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true })
                using (var reader = new StreamReader(client, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true))
                {
                    writer.WriteLine(command);
                    var response = reader.ReadLine();
                    Console.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
        return 0;
    }


    private static int HandleParseError(IEnumerable<Error> errs)
    {
        foreach (var error in errs)
        {
            Console.WriteLine(error.ToString());
        }
        return 1;
    }
}
