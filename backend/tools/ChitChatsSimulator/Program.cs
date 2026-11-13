namespace ChitChatsSimulator;

// Usage: dotnet run --project tools/ChitChatsSimulator   (listens on http://localhost:5090)
// Then point the API at it:
//   ChitChats__BaseUrl=http://localhost:5090/api/v1  ChitChats__ClientId=123456  ChitChats__AccessToken=sim-token
internal static class SimulatorHost
{
    private static void Main(string[] args)
    {
        var app = Simulator.Build(args);
        if (string.IsNullOrEmpty(app.Configuration["urls"])) app.Urls.Add("http://localhost:5090");
        Console.WriteLine($"Chit Chats simulator — access token: {Simulator.DefaultToken}");
        app.Run();
    }
}
