$ConfigPath = ".\tools.config.json"
$DefaultConfig = @{
    GameData = "C:\Users\$env:USERNAME\AppData\LocalLow\Deimos Interactive\Mechanica"
    GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Mechanica"
}

if (-Not (Test-Path $ConfigPath)) {
    Write-Host "ERROR: tools.config.json not found !" -ForegroundColor Red
    Write-Host "HELP: Please create a tools.config.json file with the following content:" -ForegroundColor Cyan
    Write-Host ($DefaultConfig | ConvertTo-Json -Depth 3) -ForegroundColor Cyan
    exit 1
}

$config = Get-Content ".\tools.config.json" | ConvertFrom-Json

$pluginsDir = Join-Path $config.GameDir "BepInEx\plugins"
if (-Not (Test-Path $pluginsDir)) {
    Write-Host "ERROR: Game not installed or BepInEx not installed or BepInEx not initialized !" -ForegroundColor Red
    Write-Host "HELP: Please make sure the game is installed, BepInEx is installed and initialized (run the game at least once with BepInEx installed)." -ForegroundColor Cyan
    exit 1
}   

dotnet build -c Release
Copy-Item "bin\Release\netstandard2.1\MechanicaMultiplayerFix.dll" $pluginsDir -Force