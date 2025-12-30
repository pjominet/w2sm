# Witcher 2 Script Merger

A script merger and mod manager for **The Witcher 2: Assassins of Kings**, inspired by the popular [Script Merger for The Witcher 3](https://www.nexusmods.com/witcher3/mods/484).

## Overview

When modding The Witcher 2, multiple mods often modify the same script files (`.ws` files inside `.dzip` archives). Installing these mods by simply overwriting files means only one mod's changes will take effect. This tool solves that problem by:

- **Detecting conflicts** between mods that modify the same scripts
- **Auto-merging** non-conflicting changes automatically
- **Providing diff views** for manual conflict resolution
- **Installing merged files** to the correct game directories

## Features

- **Native .dzip support** - Read and write Witcher 2's `.dzip` archive format without external tools
- **Multi-format archive support** - Load mods from `.zip`, `.7z`, and `.rar` files
- **Three-way merge** - Intelligently merge changes from multiple mods against vanilla scripts
- **Conflict detection** - Automatically identify which scripts have conflicting modifications
- **Integrated merge editor** - Side-by-side diff viewer with syntax highlighting and editable merge result
- **Flexible installation** - Install to UserContent (Documents) or CookedPC (Game folder)
- **Portable** - Configuration and logs saved alongside the executable, no registry or AppData clutter

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (or later)
- The Witcher 2: Assassins of Kings

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

Click **+ Add Mods** and select one or more mod archives (`.zip`, `.7z`, or `.rar` files).

The tool will:
- Extract and scan all files in each archive
- Identify `.ws` script files
- Check for conflicts with vanilla game scripts
- Check for conflicts between the selected mods

### Step 3: Review Conflicts

The **Script Conflicts** panel shows all detected conflicts:

- 🟠 **Orange** - Pending (not yet merged)
- 🟢 **Green** - Auto-merged successfully
- 🔵 **Blue** - Manually merged
- 🔴 **Red** - Failed to merge

Click on a conflict to select it, then:
- **Open Merge Editor** - Launch the integrated side-by-side diff viewer with editable merge result
- **View Diff** - Quick preview of all versions in the text panel

### Step 4: Auto-Merge

Click **Auto-Merge All** to attempt automatic merging of all conflicts.

The tool uses three-way merge logic:
- If changes don't overlap, they're merged automatically
- If changes conflict (same lines modified differently), manual intervention is needed

### Step 5: Choose Install Location

Select where to install the merged files:

- **UserContent (Documents)** - `Documents\Witcher 2\UserContent`
  - Recommended for most mods
  - Easier to manage and remove
  
- **CookedPC (Game Folder)** - `<GamePath>\CookedPC`
  - Required for some mods
  - Overwrites vanilla files (backup recommended)

### Step 6: Install

Click **Install Merged Files** to:
- Copy all non-conflicting mod files to the target location
- Install merged scripts for resolved conflicts

## File Locations

| Location | Path | Purpose |
|----------|------|---------|
| Game Scripts | `<GamePath>\CookedPC\*.dzip` | Vanilla game scripts |
| UserContent | `Documents\Witcher 2\UserContent` | User-installed mods |
| Config | `<AppFolder>\config.json` | Application settings |

## Troubleshooting

### "Invalid game path"
Make sure you selected the main Witcher 2 folder, not a subfolder. The folder should contain `bin` and `CookedPC` directories.

### Merge conflicts not auto-resolving
Some conflicts require manual intervention when multiple mods change the same lines of code. Use the integrated merge editor to manually resolve these changes.

## Technical Details

### Supported File Types

| Extension | Type | Support |
|-----------|------|---------|
| `.ws` | WitcherScript | Full merge support |
| `.dzip` | Archive | Native read/write |
| `.xml` | XML Config | Planned |
| `.w2strings` | Localization | Planned |

### Architecture

- **WPF** with MVVM pattern (CommunityToolkit.Mvvm)
- **SharpCompress** for archive handling
- **DiffPlex** for diff/merge algorithms
- **Native .dzip** implementation (no external tools required)

## Credits & Acknowledgments

- Inspired by [Witcher 3 Script Merger](https://www.nexusmods.com/witcher3/mods/484) by AnotherSymbiworkaround
- .dzip format research based on [Gibbed.RED](https://github.com/gibbed/Gibbed.RED) by Rick (gibbed)
- Reference: [zpangwin's Witcher 2 Mod Merges](https://github.com/zpangwin/zpangwin-witcher2-mod-merges)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Roadmap

- [ ] XML file merging support
- [ ] Localization file (.w2strings) support
- [ ] Mod profiles/presets
- [ ] Backup and restore functionality
- [ ] Drag-and-drop mod loading
