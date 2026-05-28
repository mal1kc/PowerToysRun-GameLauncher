# GameLauncher — PowerToys Run Plugin

Launch Steam, Epic Games, and Xbox games directly from PowerToys Run.

## Usage

Open PowerToys Run (`Alt+Space`) and type:

```
gl <game name>
```

Example: `gl witcher` shows all matching games across all platforms.  
Press `Enter` to launch. The result subtitle shows the platform and URI.

## How it works

| Platform   | Discovery                                       | Launch URI                                         |
|------------|--------------------------------------------------|-----------------------------------------------------|
| **Steam**  | Reads `steamapps/appmanifest_*.acf` files        | `steam://rungameid/<appid>`                         |
| **Epic**   | Reads `%ProgramData%\Epic\...\Manifests\*.item`  | `com.epicgames.launcher://apps/...?action=launch`   |
| **Xbox**   | Scans `XboxGames\` on all fixed drives           | `xbox://game-activity/launch/<identity>`            |

## Installation

### Manual
1. Build the project (see below).
2. Copy the output folder to:  
   `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\GameLauncher`
3. Restart PowerToys.

### Build
```powershell
# One-shot build + install:
.\build.ps1 -Install

# Build only:
.\build.ps1 -PTPath "$env:LOCALAPPDATA\PowerToys"
```

> `PTPath` must point to your PowerToys installation so the project can reference the PowerToys DLLs.

### Prerequisites
- .NET 10 SDK
- PowerToys installed (user scope: `%LOCALAPPDATA%\PowerToys` or global scope: `%ProgramW6432%\PowerToys`)

## Project structure

```
GameLauncher/
├── GameLauncher.sln
├── build.ps1          # build & install
├── debug.ps1          # debug build + hot-copy
└── GameLauncher/
    ├── GameLauncher.csproj
    ├── Main.cs
    ├── plugin.json
    └── Images/
        ├── gamelauncher.dark.png
        └── gamelauncher.light.png
```

## Icons

Place 32×32 PNG icons at:
- `Images/gamelauncher.dark.png`  (white/light icon for dark theme)
- `Images/gamelauncher.light.png` (dark icon for light theme)

You can use any gaming-related icon or create one from the Steam / gamepad SVGs.
