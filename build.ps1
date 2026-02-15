$ConfigPath = ".\tools.config.json"
$DefaultConfig = @{
    GameData = "C:\Users\$env:USERNAME\AppData\LocalLow\Deimos Interactive\Mechanica"
    GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Mechanica"
}

$csproj = Get-ChildItem -Path . -Filter "*.csproj" | Select-Object -First 1
$projName = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name)

$OutputDir = Join-Path (Get-Location) "bin\Release\netstandard2.1"
$OutPutFile = Join-Path $OutputDir "$projName.dll"


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

# Build the project
dotnet build -c Release

if ($LASTEXITCODE -eq 0) {
    Copy-Item $OutPutFile $pluginsDir -Force
    if ($?) {
        Write-Host "Build successful and DLL copied to plugins directory -> $pluginsDir" -ForegroundColor Green
    } else {
        Write-Host "Build successful but failed to copy DLL to plugins directory." -ForegroundColor Red
    }
} else {
    Write-Host "Build failed with exit code $LASTEXITCODE." -ForegroundColor Red
}
