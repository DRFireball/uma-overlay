using System.Net.WebSockets;
using System.Text;
using UmaCompanion.Core.Live;
using Xunit;

namespace UmaCompanion.Core.Tests;

public class LiveStateServerTests
{
    private const string CareerJson = """
        { "v": 1, "type": "career_turn", "payload": {
            "turn": 7, "stats": {"speed":100,"stamina":100,"power":100,"guts":100,"wiz":100},
            "energy": 80, "mood": 3, "skill_points": 10, "fans": 5, "goal_race_id": null,
            "training_options": [] } }
        """;

    [Fact]
    public async Task Server_ReceivesAndParsesFrame_FromRealWebSocketClient()
    {
        var received = new TaskCompletionSource<GameFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new LiveStateServer(port: 0, onFrame: f => received.TrySetResult(f));
        server.Start();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await client.SendAsync(Encoding.UTF8.GetBytes(CareerJson), WebSocketMessageType.Text, true, CancellationToken.None);

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var career = Assert.IsType<CareerTurnFrame>(frame);
        Assert.Equal(7, career.Turn);
    }

    [Fact]
    public async Task Server_IgnoresMalformedFrame_AndKeepsProcessingNextFrame()
    {
        var received = new TaskCompletionSource<GameFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new LiveStateServer(port: 0, onFrame: f => received.TrySetResult(f));
        server.Start();

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await client.SendAsync(Encoding.UTF8.GetBytes("garbage not json"), WebSocketMessageType.Text, true, CancellationToken.None);
        await client.SendAsync(Encoding.UTF8.GetBytes(CareerJson), WebSocketMessageType.Text, true, CancellationToken.None);

        var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsType<CareerTurnFrame>(frame);
    }
}
