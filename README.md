# Uma Companion

A passive companion app for **Umamusume: Pretty Derby** (Steam Global). It watches the game window
and tells you which race trophies you are still missing, and when those races run.

It never touches the game process, never injects anything, and never sends input. All it does is
capture the window it is told to watch (Windows.Graphics.Capture) and read the pixels with the OCR
engine already built into Windows.

---

## Install

1. Download `UmaCompanion-<version>-win-x64.zip` from the
   [Releases page](https://github.com/4-Fireball/uma-overlay/releases).
2. Unzip it and run `UmaCompanion.exe`.

That is the whole install. The .NET runtime is bundled, so there is nothing to install first.

Windows will likely show a **"Windows protected your PC"** SmartScreen warning the first time,
because the executable is not code-signed. Click *More info* → *Run anyway*, or right-click the
file → Properties → Unblock.

**Requirements**

- Windows 10 2004 (build 19041) or newer — Windows.Graphics.Capture needs it
- Umamusume installed and run at least once, so its reference data exists on disk
- An English OCR language pack (Windows has one by default in en-US installs; without it the app
  still runs but the screen reader stays off and says so in the status bar)

## Using it

Start the game, then start Uma Companion — order doesn't matter, it waits for the window and picks
it up on its own. The footer tells you what it is doing.

Two screens are read, and only these two:

- **The Trophy Room.** Browse through the pages once and every trophy you already own is checked
  off. This is how you seed your history — the app has no way to know what you won before you
  installed it. Swipe slowly enough that each page is on screen for a moment; a trophy is only
  recorded after it has been seen on two frames in a row.
- **The race result screen.** New wins are recorded as you get them.

Everything else you pass through is ignored on purpose. A fuzzy text matcher let loose on arbitrary
frames finds races that aren't there, so nothing is read from a screen that hasn't positively
identified itself.

The **Missing** tab lists the trophies you still need with the career turn each race runs on; the
**Won** tab lists what you have. *Reset wins* clears everything and lets you re-sync from scratch.

Your wins are stored at `%LOCALAPPDATA%\UmaCompanion\won-races.json`.

**Known gap:** the trophy total reads 155 against the game's 193. The G2 and G3 tabs match the game
exactly; the EX tab (~41 dirt and exhibition trophies) isn't modelled yet, so those races never
appear as missing.

---

## Development

Static game data comes from the game's own SQLite database, `master.mdb`, at
`%AppData%\LocalLow\Cygames\Umamusume\master\master.mdb`. It is opened read-only via a snapshot
copy and is refreshed by the game each patch. Set `UMA_MASTER_MDB` to point somewhere else.

Internally the screen reader is just one producer of the frame contract in
[`docs/ws-contract.md`](docs/ws-contract.md); the same frames can arrive over a local WebSocket,
which is how the app is developed with the game closed (`tools/FakeHookSender`).

### Projects

| Project | Target | Builds on | Purpose |
|---|---|---|---|
| `src/UmaCompanion.Core` | `net8.0` | Linux / Windows | Data layer + engines (master.mdb reader, schedule/trophy/skills, OCR interpreters). No UI deps. |
| `tests/UmaCompanion.Core.Tests` | `net8.0` | Linux / Windows | xUnit tests for Core. |
| `src/UmaCompanion.App` | `net8.0-windows*` | **Windows only** (WPF) | Companion window, WebSocket server, screen-reader capture. |
| `tests/UmaCompanion.App.Tests` | `net8.0-windows*` | **Windows only** | Real-OCR pipeline tests against captured game screens. |
| `tools/FakeHookSender` | `net8.0` | Linux / Windows | Dev tool: replays fixture frames into the app. |

The Core library and its tests are cross-platform. The WPF app compiles everywhere (it sets
`EnableWindowsTargeting`, so `dotnet build` works from WSL) but only *runs* on Windows.

### Build & test

```powershell
dotnet test                      # everything; integration tests skip without master.mdb
$env:UMA_MASTER_MDB = "$env:USERPROFILE\AppData\LocalLow\Cygames\Umamusume\master\master.mdb"
dotnet test                      # also runs the real-master.mdb and OCR fixture tests
dotnet run --project src\UmaCompanion.App
```

Set `UMA_MASTER_MDB` before you trust a green run — several tests silently skip without it,
including the ones that catch false trophy detections.

From WSL (Core only; the box lacks ICU, so .NET runs invariant-globalization):

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$PATH:$HOME/.dotnet
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_NOLOGO=1
dotnet test tests/UmaCompanion.Core.Tests
```

### Test fixtures

`tests/fixtures/*.png` are real captures of game screens, and the tests assert exact results
against them — the precise set of owned trophies on a page, or that a screen produces *no*
detection at all. Both directions matter: several past bugs were extra detections, not missing
ones. When you fix a detection bug, add the capture that exposed it.

### Releasing

```powershell
.\publish.ps1                    # builds artifacts\UmaCompanion-<version>-win-x64.zip locally
```

To cut a real release, bump `<Version>` in `src/UmaCompanion.App/UmaCompanion.App.csproj`, then:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

[`.github/workflows/release.yml`](.github/workflows/release.yml) builds, tests, and attaches the
zip to a GitHub Release. Both paths use the same publish profile
(`src/UmaCompanion.App/Properties/PublishProfiles/win-x64.pubxml`), so a local build and a released
build are the same artifact.
