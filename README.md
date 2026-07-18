# Uma Companion

Out-of-process companion app for Umamusume: Pretty Derby (Steam Global). Surfaces GameTora-style
card/skill reference, per-turn training recommendations, missing race trophies, and aptitude-based
race schedules. Live game state arrives from a separately-built hook over a local WebSocket (see
[`docs/ws-contract.md`](docs/ws-contract.md)); the app itself never touches the game process.

Full scope & decisions: `~/.claude/plans/i-d-like-to-scope-zany-corbato.md`.

## Projects
| Project | Target | Builds on |
|---|---|---|
| `src/UmaCompanion.Core` | `net8.0` | Linux / Windows | Data layer + engines (master.mdb reader, training/schedule/trophy). No UI deps. |
| `tests/UmaCompanion.Core.Tests` | `net8.0` | Linux / Windows | xUnit tests for Core. |
| `src/UmaCompanion.App` *(planned)* | `net8.0-windows` | **Windows only** (WPF) | Companion window + WebSocket server. |

The Core library and its tests are cross-platform and run in CI / WSL. The WPF **App** is
Windows-only and must be built and run on Windows (where the game lives).

## Build & test

This dev box (WSL, minimal) lacks ICU, so .NET runs in invariant-globalization mode:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$PATH:$HOME/.dotnet
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_NOLOGO=1
dotnet test          # builds Core + runs all tests
```

On a normal Windows/dev machine, plain `dotnet build` / `dotnet test` work without the env vars.

## Reference data
Static game data comes from the game's own SQLite DB, `master.mdb`, found at
`%AppData%\LocalLow\Cygames\Umamusume\master\master.mdb` (or the Steam
`Umamusume_Data\Persistent\master\` path). It is read-only and refreshed by the game each patch.
