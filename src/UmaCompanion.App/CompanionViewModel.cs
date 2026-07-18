using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using UmaCompanion.App.Reading;
using UmaCompanion.Core.Live;
using UmaCompanion.Core.MasterData;
using UmaCompanion.Core.Races;
using UmaCompanion.Core.Reading;
using UmaCompanion.Core.Trophies;

namespace UmaCompanion.App;

/// <summary>
/// Backs the companion window. Opens master.mdb, runs the live-state server, and keeps the
/// missing-trophy schedule in sync with the races the current character has won. Live frames
/// arrive on background threads and are marshalled onto the UI dispatcher before touching state.
/// </summary>
public sealed class CompanionViewModel : INotifyPropertyChanged, IDisposable
{
    // Port fixed by the live-state contract (docs/ws-contract.md).
    private const int ListenPort = 8974;

    private readonly Dispatcher _dispatcher;
    private readonly HashSet<int> _wonRaceIds = new();

    private MasterDb? _db;
    private TrophyPlanner? _planner;
    private LiveStateServer? _server;
    private ScreenReaderService? _screenReader;

    public CompanionViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>The current missing-trophy schedule, one row per career turn slot.</summary>
    public ObservableCollection<ScheduleRow> Schedule { get; } = new();

    private string _status = "Starting…";
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    private string _masterDbPath = "";
    public string MasterDbPath
    {
        get => _masterDbPath;
        private set => Set(ref _masterDbPath, value);
    }

    private string _hookActivity = "No frames received yet.";
    public string HookActivity
    {
        get => _hookActivity;
        private set => Set(ref _hookActivity, value);
    }

    private string _captureStatus = "Screen reader starting…";
    public string CaptureStatus
    {
        get => _captureStatus;
        private set => Set(ref _captureStatus, value);
    }

    private int _missingCount;
    public int MissingCount
    {
        get => _missingCount;
        private set => Set(ref _missingCount, value);
    }

    private int _totalTrophies;
    public int TotalTrophies
    {
        get => _totalTrophies;
        private set => Set(ref _totalTrophies, value);
    }

    private int _wonCount;
    public int WonCount
    {
        get => _wonCount;
        private set => Set(ref _wonCount, value);
    }

    /// <summary>Locates master.mdb, loads the trophy reference data, and starts the live server.</summary>
    public void Start()
    {
        var path = MasterMdbLocator.Locate();
        if (path is null)
        {
            Status = "Could not find master.mdb. Launch the game once, or set UMA_MASTER_MDB.";
            return;
        }

        try
        {
            _db = MasterDb.OpenReadOnlyCopy(path);
            _planner = TrophyPlanner.FromMasterDb(_db);
            MasterDbPath = path;
            TotalTrophies = _planner.MissingTrophies(_wonRaceIds).Count; // no wins yet ⇒ all trophies
            Recompute();

            _server = new LiveStateServer(ListenPort, OnFrame);
            _server.Start();
            Status = $"Listening on ws://localhost:{ListenPort} — waiting for the game hook.";

            // The screen reader is just a second producer of the same frames the server ingests.
            var raceNames = _db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
                .Concat(_db.GetTrophyRaces().Select(t => (t.RaceId, t.Name)));
            _screenReader = new ScreenReaderService(
                new RaceResultScreenInterpreter(new RaceNameMatcher(raceNames)),
                OnFrame,
                status => _dispatcher.InvokeAsync(() => CaptureStatus = status));
            _screenReader.Start();
        }
        catch (Exception ex)
        {
            Status = $"Startup failed: {ex.Message}";
        }
    }

    // Invoked on a background thread by LiveStateServer; hop to the UI thread before touching state.
    private void OnFrame(GameFrame frame)
    {
        _dispatcher.InvokeAsync(() => Apply(frame));
    }

    private void Apply(GameFrame frame)
    {
        switch (frame)
        {
            case RaceResultsFrame results:
                var newWins = 0;
                foreach (var r in results.Results)
                {
                    if (r.Won && _wonRaceIds.Add(r.RaceId))
                    {
                        newWins++;
                    }
                }
                HookActivity = $"race_results: {results.Results.Count} race(s), {newWins} new win(s).";
                Recompute();
                break;

            case CareerTurnFrame turn:
                HookActivity = $"career_turn: turn {turn.Turn}, {turn.SkillPoints} SP, {turn.Fans:N0} fans.";
                break;
        }
    }

    private void Recompute()
    {
        if (_planner is null)
        {
            return;
        }

        WonCount = _wonRaceIds.Count;
        MissingCount = _planner.MissingTrophies(_wonRaceIds).Count;

        Schedule.Clear();
        foreach (var scheduled in _planner.MissingTrophySchedule(_wonRaceIds))
        {
            Schedule.Add(ScheduleRow.From(scheduled));
        }
    }

    public void Dispose()
    {
        _screenReader?.Dispose();
        _server?.Dispose();
        _db?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>A single row in the missing-trophy schedule, pre-formatted for display.</summary>
public sealed class ScheduleRow
{
    private static readonly string[] MonthAbbrev =
        { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    private ScheduleRow(string slot, string race, string grade, string distance)
    {
        Slot = slot;
        Race = race;
        Grade = grade;
        Distance = distance;
    }

    public string Slot { get; }
    public string Race { get; }
    public string Grade { get; }
    public string Distance { get; }

    public static ScheduleRow From(ScheduledRace scheduled)
    {
        var race = scheduled.Race;
        var month = race.Month >= 1 && race.Month <= 12 ? MonthAbbrev[race.Month] : race.Month.ToString();
        var slot = $"{race.Year} · {month} {race.Half}";
        var distance = $"{race.Distance}m {race.Surface}";
        return new ScheduleRow(slot, race.Name, race.Grade.ToString(), distance);
    }
}
