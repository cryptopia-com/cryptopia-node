using Cryptopia.Node.Client.RPC;

public class Program
{ 
    public static async Task Main(string[] args)
    {
        var client = new RpcClient("127.0.0.1", 5000);

        try
        {
            await client.ConnectAsync();

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (input == "exit") break;

                var result = await client.SendCommandAsync(input);
                Console.WriteLine(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }
}