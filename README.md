# Witcher 2 Script Merger

A script merger and mod manager for **The Witcher 2: Assassins of Kings**, inspired by the popular [Script Merger for The Witcher 3](https://www.nexusmods.com/witcher3/mods/484).

## Overview

When modding The Witcher 2, multiple mods often modify the same script files (`.ws` files inside `.dzip` archives). Installing these mods by simply overwriting files means only one mod's changes will take effect. This tool solves that problem by:

- **Detecting conflicts** between mods that modify the same scripts
- **Auto-merging** non-conflicting changes automatically
- **Providing diff views** for manual conflict resolution
- **Installing merged files** to the correct game directories
- **Cleaning-up mod archives** so that mod archives with weird folder structures are recognized and properly installed 

## Features

### Core Features
- **Native .dzip support** - Read and write Witcher 2's `.dzip` archive format, no need for Gibbed.RED tools
- **Multi-format archive support** - Load mods from `.zip`, `.7z`, and `.rar` files
- **Auto merge** - Intelligently merge changes from multiple mods against vanilla scripts if possible
- **Flexible installation** - Install to UserContent (Documents) or CookedPC (Game folder)
- **Portable** - Configuration and logs saved alongside the executable, no registry or AppData clutter

## Installation

1. Download the latest release
2. Extract to any folder
3. Run `W2ScriptMerger.exe`

The application is fully portable - just copy the folder anywhere you like.

## Usage Guide

### Step 1: Set Game Path

On first launch, click **Browse...** and select your Witcher 2 installation folder (the folder containing `bin` and `CookedPC` directories).

Example paths:
- Steam: `C:\Program Files (x86)\Steam\steamapps\common\The Witcher 2`
- GOG: `C:\GOG Games\The Witcher 2`

### Step 2: Add Mods

Click **+ Add Mods** and select one or more mod archives.

The tool will:
- Extract and scan all files in each archive
- Identify `.ws` script files
- Check for conflicts with vanilla game scripts
- Check for conflicts between the selected mods

### Step 3: Review Conflicts

The **Script Conflicts** panel shows all detected conflicts as a tree view:

- 🔴 **Red** - Unresolved conflict
- 🟠 **Orange** - Needs manual resolution
- 🟢 **Green** - Auto-merged successfully
- 🔵 **Blue** - Manually resolved

Only scripts with actual changes are shown.

Click on a conflict to select it, open readonly diff viewer to inspect potential changes

### Step 4: Auto-Merge

Click **Auto-Merge All** to attempt automatic merging of all conflicts.

The manager uses three-way merge logic:
- If changes don't overlap, they're merged automatically
- If changes conflict (same lines modified differently), manual intervention is requested

### Step 5: Choose Install Location

Select where to install the merged files:

- **UserContent (Documents)** - `Documents\Witcher 2\UserContent`
  
- **CookedPC (Game Folder)** - `<GamePath>\CookedPC`

### Step 6: Install

Click **Install Merged Files** to:
- Copy all non-conflicting mod files to the game folder
- Copy merged scripts for resolved conflicts to the game folder
- The manager creates automatic backups of the original files in the game folder

## Troubleshooting

### "Invalid game path"
Make sure you selected the main Witcher 2 folder, not a subfolder. The folder should contain `bin` and `CookedPC` directories.

## Credits & Acknowledgments

- Inspired by [Witcher 3 Script Merger](https://www.nexusmods.com/witcher3/mods/484) by AnotherSymbiworkaround and [Script Merger - Fresh and Automated Edition](https://www.nexusmods.com/witcher3/mods/8405) by Phaz42
- .dzip format research based on [Gibbed.RED](https://github.com/gibbed/Gibbed.RED) by Rick (gibbed)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Roadmap

- [ ] XML file merging support
- [ ] Localization file (.w2strings) support
- [ ] Mod profiles/presets
- [ ] Drag-and-drop mod loading
- [ ] Nexus Mods integration
