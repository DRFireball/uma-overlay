using System.Net.WebSockets;
using System.Text;

// Dev tool: pretends to be the game hook. Connects to the companion app's WebSocket server and
// replays fixture JSON frames, so the whole app is developable/testable with the game closed.
//
//   dotnet run --project tools/FakeHookSender -- [ws://127.0.0.1:8974/] [fixtureFileOrDir]

var url = args.Length > 0 ? args[0] : "ws://127.0.0.1:8974/";
var source = args.Length > 1 ? args[1] : "tests/fixtures";

var files = Directory.Exists(source)
    ? Directory.GetFiles(source, "*.json").OrderBy(f => f).ToArray()
    : new[] { source };

using var client = new ClientWebSocket();
Console.WriteLine($"Connecting to {url} ...");
await client.ConnectAsync(new Uri(url), CancellationToken.None);
Console.WriteLine("Connected.");

foreach (var file in files)
{
    var json = await File.ReadAllTextAsync(file);
    await client.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    Console.WriteLine($"Sent {Path.GetFileName(file)} ({json.Length} bytes)");
    await Task.Delay(200);
}

try
{
    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
}
catch (WebSocketException)
{
    // Server may have already closed the socket (e.g. it processed our frames and exited); ignore.
}
Console.WriteLine("Done.");
