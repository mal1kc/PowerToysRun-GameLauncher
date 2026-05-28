# build.ps1 — Build and install GameLauncher plugin
param(
    [string]$PTPath = "$env:LOCALAPPDATA\PowerToys",
    [switch]$Install
)

$pluginDir = "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\GameLauncher"
$outDir    = "GameLauncher\bin\x64\Release\net9.0-windows"

Write-Host "Building GameLauncher plugin..." -ForegroundColor Cyan
dotnet build GameLauncher\GameLauncher.csproj -c Release -p:Platform=x64 -p:PTPath="$PTPath"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

if ($Install) {
    Write-Host "Stopping PowerToys..." -ForegroundColor Yellow
    Get-Process -Name PowerToys -ErrorAction SilentlyContinue | Stop-Process -Force

    Write-Host "Installing to $pluginDir ..." -ForegroundColor Cyan
    if (Test-Path $pluginDir) { Remove-Item $pluginDir -Recurse -Force }
    New-Item -ItemType Directory -Path $pluginDir | Out-Null
    Copy-Item "$outDir\*" $pluginDir -Recurse

    Write-Host "Restarting PowerToys..." -ForegroundColor Green
    Start-Process "$PTPath\PowerToys.exe"
    Write-Host "Done! Use 'gl <game name>' in PowerToys Run." -ForegroundColor Green
}
