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

    // Race id -> display info, so a recorded win id can be shown as a name even when the race is
    // no longer in the missing list. Populated once from master.mdb in Start().
    private readonly Dictionary<int, (string Name, string Grade)> _raceLookup = new();

    private MasterDb? _db;
    private TrophyPlanner? _planner;
    private LiveStateServer? _server;
    private ScreenReaderService? _screenReader;
    private WonRaceStore? _wonStore;

    public CompanionViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>The current missing-trophy schedule, one row per career turn slot.</summary>
    public ObservableCollection<ScheduleRow> Schedule { get; } = new();

    /// <summary>Every race recorded as won, newest sync order irrelevant — sorted by name.</summary>
    public ObservableCollection<WonRow> Won { get; } = new();

    private bool _showWon;

    /// <summary>Which tab is selected. The two lists share the same grid cell.</summary>
    public bool ShowWon
    {
        get => _showWon;
        private set
        {
            if (!Set(ref _showWon, value))
            {
                return;
            }
            Raise(nameof(MissingListVisibility));
            Raise(nameof(WonListVisibility));
            Raise(nameof(MissingTabBackground));
            Raise(nameof(WonTabBackground));
        }
    }

    public Visibility MissingListVisibility => _showWon ? Visibility.Collapsed : Visibility.Visible;
    public Visibility WonListVisibility => _showWon ? Visibility.Visible : Visibility.Collapsed;

    // Selected tab reads as a raised surface; the other blends into the window.
    public string MissingTabBackground => _showWon ? "#FF20232C" : "#FF313747";
    public string WonTabBackground => _showWon ? "#FF313747" : "#FF20232C";

    public string MissingTabLabel => $"Missing ({MissingCount})";
    public string WonTabLabel => $"Won ({WonCount})";

    public void ShowMissingTab() => ShowWon = false;
    public void ShowWonTab() => ShowWon = true;

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

    private string _hookActivity = "WebSocket (ws://localhost:8974): no frames yet — optional, the screen reader works without it.";
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
        private set
        {
            if (Set(ref _missingCount, value))
            {
                Raise(nameof(MissingTabLabel));
            }
        }
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
        private set
        {
            if (Set(ref _wonCount, value))
            {
                Raise(nameof(WonTabLabel));
            }
        }
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
            TotalTrophies = _planner.MissingTrophies(Array.Empty<int>()).Count;

            // Career races first so a trophy entry's (canonical) name wins any id collision.
            foreach (var race in _db.GetCareerRaces())
            {
                _raceLookup[race.RaceId] = (race.Name, race.Grade.ToString());
            }
            foreach (var trophy in _db.GetTrophyRaces())
            {
                _raceLookup[trophy.RaceId] = (trophy.Name, trophy.Grade.ToString());
            }

            // Wins persist across sessions (result screens + Trophy Room sync both land here).
            _wonStore = WonRaceStore.CreateDefault();
            _wonRaceIds.UnionWith(_wonStore.Load());
            Recompute();

            _server = new LiveStateServer(ListenPort, OnFrame);
            _server.Start();
            Status = $"Ready — {TotalTrophies} trophy races loaded. " +
                "Browse the game's Trophy Room to sync past wins.";

            // The screen reader is just a second producer of the same frames the server ingests.
            var raceNames = _db.GetCareerRaces().Select(r => (r.RaceId, r.Name))
                .Concat(_db.GetTrophyRaces().Select(t => (t.RaceId, t.Name)));
            var matcher = new RaceNameMatcher(raceNames);
            _screenReader = new ScreenReaderService(
                new RaceResultScreenInterpreter(matcher),
                new TrophyRoomScreenInterpreter(matcher),
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
                if (newWins > 0)
                {
                    _wonStore?.Save(_wonRaceIds);
                }
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

        // An id with no lookup entry is still worth showing — it means a detection landed on a
        // race outside the trophy set, which is exactly what we'd want to see while debugging.
        Won.Clear();
        foreach (var row in _wonRaceIds
            .Select(id => _raceLookup.TryGetValue(id, out var info)
                ? new WonRow(id, info.Name, info.Grade)
                : new WonRow(id, $"(unknown race {id})", "—"))
            .OrderBy(r => r.Race, StringComparer.OrdinalIgnoreCase))
        {
            Won.Add(row);
        }
    }

    /// <summary>
    /// Clears every recorded win — memory, the persisted store, and the screen reader's
    /// session dedup — so the tracker starts fresh. Re-browsing the Trophy Room re-syncs.
    /// </summary>
    public void ResetWins()
    {
        _wonRaceIds.Clear();
        _wonStore?.Save(_wonRaceIds);
        _screenReader?.ResetSession();
        HookActivity = "Wins reset — browse the Trophy Room to re-sync.";
        Recompute();
    }

    public void Dispose()
    {
        _screenReader?.Dispose();
        _server?.Dispose();
        _db?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void Raise(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A race recorded as won, as shown on the Won tab.</summary>
public sealed class WonRow
{
    public WonRow(int raceId, string race, string grade)
    {
        RaceId = raceId;
        Race = race;
        Grade = grade;
    }

    public int RaceId { get; }
    public string Race { get; }
    public string Grade { get; }
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
