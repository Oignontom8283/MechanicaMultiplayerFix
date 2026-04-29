
# Mechanica Multiplayer Fix

**Mechanica Multiplayer Fix** is a [BepInEx](https://github.com/bepinex/bepinex) mod
for the game [Mechanica](https://store.steampowered.com/app/1226990/Mechanica/), a survival game mixing survival and programming developed by 
**Deimos Interactive**, with development abandoned since February 2022.
This mod attempts to fix instability / connectivity issues in the game's multiplayer mode.

Currently, the results are mixed; the game's code structure is very complex, not due to sophistication, but rather poor design,
which makes the patching task very difficult.
It's **simple**: the game's code would need to be entirely rewritten to fix the conceptual problems of both multiplayer and single-player modes.

This mod fixes some issues, but not all, and my capacity to perform tests is very limited.
For now, it seems the current mod version provides an improvement in multiplayer stability, but it is hard to be certain.
So, do not take this mod as a miracle solution.

I want to note that the mod does not solve synchronization issues at all (especially regarding ports, which are still as out of sync as ever),
and the game's multiplayer mode remains very unstable, even with this mod.

## Installation

### 1. Own the game.

legally, please.

## 1. Install BepInEx

- Download [BepInEx 5.4.23.5 win_x86](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
You should be able to use any version of BepInEx 5,
but I recommend using the same one used for the mod's development.

- Extract the archive's content into the game's installation folder. By default, it's `C:\Program Files (x86)\Steam\steamapps\common\Mechanica\`.

- Launch the game once so BepInEx can create the necessary folders, then close the game.

## 2. Install the mod

- Download the latest version of the mod [here](https://github.com/Oignontom8283/MechanicaMultiplayerFix/releases). It comes as a `.dll` file.

- Copy the `.dll` file into the game's `BepInEx\plugins` folder. By default, it's `C:\Program Files (x86)\Steam\steamapps\common\Mechanica\BepInEx\plugins`.

## 3. Done

## Contribution

If you wish to contribute to the project, peace be upon your soul, you can fork the project and make a pull request.
The code is quite messy and experimental (draft) so yeah, sorry.

### Environment

I do not use Visual Studio, I don't like that software. So it's very simple.

### 1. Have the game installed on your computer, legally, please.

### 2. Install BepInEx (see the "Installation" section above)

### 3. .NET SDK

Install the .NET development SDK (to dev in C#):
```
winget install Microsoft.DotNet.SDK.10
```

Verify that the SDK is properly installed:
```
dotnet --version
```

### 4. Clone your fork of the project

### 5. Compilation

You can absolutely use the command `dotnet build -c Release` to compile the project,
but I recommend using the `build.ps1` script which compiles the project and copies the `.dll` file into the game's `BepInEx\plugins` folder.
On the first run, the script will ask you to create a config file to indicate the game's location to the script,
with a default content proposal.

### 6. Launch the game

I advise you to enable the BepInEx console to see the mod logs, and thus be able to debug more easily.
It's a simple rule to change in the `BepInEx\config\BepInEx.cfg` file.

## License

This project is licensed under LGPL-3.0, see the [LICENSE](./LICENSE) file for more information.

## Acknowledgments

I really thank all the people who helped me test the mod, they will recognize themselves (: