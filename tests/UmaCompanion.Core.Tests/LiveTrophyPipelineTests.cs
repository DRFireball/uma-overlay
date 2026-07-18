using System.Net.WebSockets;
using System.Text;
using UmaCompanion.Core.Live;
using UmaCompanion.Core.Races;
using UmaCompanion.Core.Trophies;
using Xunit;

namespace UmaCompanion.Core.Tests;

/// <summary>
/// End-to-end test of the backbone the WPF companion shell drives: a race_results frame arriving
/// over the live WebSocket updates the set of won races, which shrinks the missing-trophy schedule.
/// Exercises <see cref="LiveStateServer"/> + <see cref="FrameParser"/> + <see cref="TrophyPlanner"/>
/// together, headless, so the shell's wiring is covered without needing a display or the game.
/// </summary>
public class LiveTrophyPipelineTests
{
    private static TrophyPlanner BuildPlanner()
    {
        var trophyRaces = new[]
        {
            new TrophyRace(100, "Race A", RaceGrade.G1),
            new TrophyRace(200, "Race B", RaceGrade.G1),
            new TrophyRace(300, "Race C", RaceGrade.G1),
        };
        var calendar = new[]
        {
            Career(100, 2), Career(200, 4), Career(300, 6),
        };
        return new TrophyPlanner(trophyRaces, calendar);
    }

    private static CareerRace Career(int raceId, int month) => new(
        RaceId: raceId, Name: $"Race {raceId}", Grade: RaceGrade.G1,
        Year: CareerYear.Classic, Month: month, Half: RaceHalf.Early,
        Distance: 2000, Surface: Surface.Turf,
        DistanceCategory: CareerRace.CategoryFor(2000), NeedFanCount: 2000);

    [Fact]
    public async Task WonRace_ArrivingOverWebSocket_DropsOutOfMissingTrophySchedule()
    {
        var planner = BuildPlanner();
        var won = new HashSet<int>();

        // Baseline: with no wins, all three trophy races are scheduled.
        Assert.Equal(3, planner.MissingTrophySchedule(won).Count);

        // Mirrors CompanionViewModel: accumulate won race ids, signal once a result frame lands.
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new LiveStateServer(port: 0, onFrame: frame =>
        {
            if (frame is RaceResultsFrame results)
            {
                foreach (var r in results.Results)
                {
                    if (r.Won)
                    {
                        won.Add(r.RaceId);
                    }
                }
                processed.TrySetResult();
            }
        });
        server.Start();

        const string raceResultsJson = """
            { "v": 1, "type": "race_results", "payload": [
                { "chara_id": 1, "race_id": 200, "won": true } ] }
            """;

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/"), CancellationToken.None);
        await client.SendAsync(
            Encoding.UTF8.GetBytes(raceResultsJson), WebSocketMessageType.Text, true, CancellationToken.None);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var schedule = planner.MissingTrophySchedule(won);
        Assert.Equal(2, schedule.Count);
        Assert.DoesNotContain(schedule, s => s.Race.RaceId == 200);
        Assert.DoesNotContain(planner.MissingTrophies(won), t => t.RaceId == 200);
    }
}
