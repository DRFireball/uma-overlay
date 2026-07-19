# Uma Companion

Private repo: <https://github.com/4-Fireball/uma-overlay> — the hub both working copies
(Windows + WSL) push to and pull from.

Companion app for Umamusume: Pretty Derby (Steam Global). Surfaces GameTora-style skill
reference/purchase recommendations, missing race trophies, and aptitude-based race schedules.

Game state is acquired **passively**: the app screenshots the game window (Windows.Graphics.Capture)
and reads it with OCR — it never touches the game process and never sends input. Internally the
screen reader is just one producer of the frame contract in
[`docs/ws-contract.md`](docs/ws-contract.md); the same frames can also arrive over a local
WebSocket, which is how the app is developed and tested with the game closed
(`tools/FakeHookSender`).

Full scope & decisions: `~/.claude/plans/i-d-like-to-scope-zany-corbato.md`.

## Projects
| Project | Target | Builds on | Purpose |
|---|---|---|---|
| `src/UmaCompanion.Core` | `net8.0` | Linux / Windows | Data layer + engines (master.mdb reader, schedule/trophy/skills, OCR interpreters). No UI deps. |
| `tests/UmaCompanion.Core.Tests` | `net8.0` | Linux / Windows | xUnit tests for Core. |
| `src/UmaCompanion.App` | `net8.0-windows*` | **Windows only** (WPF) | Companion window, WebSocket server, screen-reader capture. |
| `tools/FakeHookSender` | `net8.0` | Linux / Windows | Dev tool: replays fixture frames into the app. |

The Core library and its tests are cross-platform. The WPF **App** compiles everywhere (it sets
`EnableWindowsTargeting` so `dotnet build` works from WSL) but only *runs* on Windows, where the
game lives.

## Build & test

**Windows** (primary dev environment — repo clone at `C:\Users\Owner\projects\uma-overlay`):

```powershell
dotnet test                      # everything
$env:UMA_MASTER_MDB = "$env:USERPROFILE\AppData\LocalLow\Cygames\Umamusume\master\master.mdb"
dotnet test                      # also runs the real-master.mdb integration tests
dotnet run --project src\UmaCompanion.App
```

**WSL** (Core work; the box lacks ICU, so .NET runs invariant-globalization):

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$PATH:$HOME/.dotnet
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_NOLOGO=1
dotnet test tests/UmaCompanion.Core.Tests   # Core tests (Windows-only test projects can't run here)
```

## Reference data
Static game data comes from the game's own SQLite DB, `master.mdb`, found at
`%AppData%\LocalLow\Cygames\Umamusume\master\master.mdb` (or the Steam
`Umamusume_Data\Persistent\master\` path). It is read-only and refreshed by the game each patch.
