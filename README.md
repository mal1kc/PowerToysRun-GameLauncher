# GameLauncher — PowerToys Run Plugin

Launch Steam, Epic Games, and Xbox games from PowerToys Run.

Usage
-----
Type `gl <game name>` in PowerToys Run (Alt+Space).

Build & Install
---------------
One-shot build and install:

## Build + Installation

### Manual
1. Build the project (see below).
2. Copy the output folder to:  
   `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\GameLauncher`
3. Restart PowerToys.

### Auto with build
- Build and Install if needed specificy powertoys location
```powershell
.\build.ps1 -Install
```

- Build only:

```powershell
.\build.ps1 -PTPath "$env:LOCALAPPDATA\PowerToys"
```

## inspired from

Flow Launcher pluing = https://www.flowlauncher.com/plugins/games-launcher/  - https://github.com/KrystianLesniak/Flow.Launcher.Plugin.GamesLauncher

Notes
-----
- The plugin auto-generates simple 32×32 launcher icons (transparent background, dash with a slash) if none are provided.
- To override the defaults, place 32×32 PNGs at `GameLauncher/Images/gamelauncher.dark.png` and `GameLauncher/Images/gamelauncher.light.png`.
- Licensed under the MIT License (see `LICENSE`).

