# debug.ps1 — Build in Debug mode and hot-copy to PowerToys Plugins folder
param(
    [string]$PTPath = "$env:LOCALAPPDATA\PowerToys"
)

$pluginDir = "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\GameLauncher"
$outDir    = "GameLauncher\bin\x64\Debug\net9.0-windows"

dotnet build GameLauncher\GameLauncher.csproj -c Debug -p:Platform=x64 -p:PTPath="$PTPath"
if ($LASTEXITCODE -ne 0) { exit 1 }

Get-Process -Name PowerToys -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1

if (Test-Path $pluginDir) { Remove-Item $pluginDir -Recurse -Force }
New-Item -ItemType Directory -Path $pluginDir | Out-Null
Copy-Item "$outDir\*" $pluginDir -Recurse

Start-Process "$PTPath\PowerToys.exe"
Write-Host "Attach debugger to PowerToys.PowerLauncher" -ForegroundColor Yellow
