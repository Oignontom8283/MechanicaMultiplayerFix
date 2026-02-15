$ConfigPath = ".\tools.config.json"
$DefaultConfig = @{
    GameData = "C:\Users\$env:USERNAME\AppData\LocalLow\Deimos Interactive\Mechanica"
    GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Mechanica"
}

$csproj = Get-ChildItem -Path . -Filter "*.csproj" | Select-Object -First 1 # Get the .csproj file
if (-not $csproj) { # If no .csproj file is found, exit with an error message
    Write-Host "ERROR: No .csproj file found in the current directory!" -ForegroundColor Red
    exit 1
}
$projName = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name) # Get the project name from the .csproj file name

$OutputDir = Join-Path (Get-Location) "bin\Release\netstandard2.1"
$OutPutFile = Join-Path $OutputDir "$projName.dll"


# Check if the config file exists, if not, show an error message and the default config content, then exit
if (-Not (Test-Path $ConfigPath)) {
    Write-Host "ERROR: tools.config.json not found !" -ForegroundColor Red
    Write-Host "HELP: Please create a tools.config.json file with the following content:" -ForegroundColor Cyan
    Write-Host ($DefaultConfig | ConvertTo-Json -Depth 3) -ForegroundColor Cyan
    exit 1
}

# Try to read and parse the JSON config file, if it fails, show an error message and exit
try { 
    $Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
}
catch {
    Write-Error "Impossible de lire ou parser le fichier JSON : $($_.Exception.Message)"
    exit 1
}

# Check if Game, BepInEx and plugins dir exist, if not, show an error message and exit
$pluginsDir = Join-Path $Config.GameDir "BepInEx\plugins"
if (-Not (Test-Path $pluginsDir)) {
    Write-Host "ERROR: Game not installed or BepInEx not installed or BepInEx not initialized !" -ForegroundColor Red
    Write-Host "HELP: Please make sure the game is installed, BepInEx is installed and initialized (run the game at least once with BepInEx installed)." -ForegroundColor Cyan
    Write-Host "Expected plugins directory: $pluginsDir" -ForegroundColor Cyan
    exit 1
}


# Build the project
dotnet build -c Release

# Check if the build was successful and copy the DLL to the plugins directory
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
