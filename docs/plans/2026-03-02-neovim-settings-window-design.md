# Unified Neovim Settings Window - Design Document

**Date:** 2026-03-02
**Status:** Approved

## Overview

Create a unified `Window/Neovim` settings window that consolidates all Neovim-specific configuration currently scattered across multiple modal windows. Accessible via both Unity's menu and a button in Preferences.

## Motivation

Currently, Neovim settings are spread across 5 different modal windows accessible only via the `Neovim/` top-level menu:
- Change Terminal Launch Cmd
- Change Open-File Request Args
- Change Jump-to-Cursor-Position Request Args
- Change Process Timeout
- Reset/Server Management (recently moved to Preferences)

This creates poor UX where settings are hard to discover and configure. A unified window with tabbed navigation will:
- Improve discoverability (Window/Neovim follows Unity conventions)
- Centralize all Neovim configuration in one place
- Reduce menu clutter by removing the Neovim top-level menu

## Window Architecture

### File Structure

```
Editor/NeovimSettingsWindow.cs  (NEW)
Editor/NeovimCodeEditor.cs     (MODIFY - remove Behavior/Server sections, add button)
Editor/NeovimChangeTerminalLaunchCmd.cs           (DELETE)
Editor/NeovimChangeOpenFileRequestArgs.cs         (DELETE)
Editor/NeovimChangeJumpToCursorPositionRequestArgs.cs (DELETE)
Editor/NeovimChangeProcessTimeout.cs              (DELETE)
```

### Window Properties

| Property | Value |
|----------|-------|
| Menu Path | `Window/Neovim` |
| Title | "Neovim Settings" |
| Resizable | Yes |
| Min Size | 600×400 |
| Default Size | 750×550 |
| Base Class | `EditorWindow` |
| UI System | UIElements (UnityEngine.UIElements) |

## Tab Structure

Four tabs organize settings logically:

### Tab 1: Behavior
- **Kill Nvim on Quit** - Toggle (from Preferences)
- **Process Timeout** - Integer field in ms (from NeovimChangeProcessTimeout.cs)

### Tab 2: Terminal
- **Terminal Launch Command** - Text field with template dropdown
- **Arguments** - Text field with placeholder reference
- **Environment Variables** - Text field
- Source: `NeovimChangeTerminalLaunchCmd.cs`

### Tab 3: File Opening
- **Modifier Bindings** - Complex UI for Shift/Ctrl/Alt key combinations (from NeovimChangeOpenFileRequestArgs.cs)
- **Jump-to-Cursor Arguments** - Text field with template dropdown (from NeovimChangeJumpToCursorPositionRequestArgs.cs)

### Tab 4: Maintenance
- **Kill Orphaned Server** - Button
- **Reset Config** - Button
- **Force Reset (Kill + Reset)** - Button
- Source: From NeovimCodeEditor.OnGUI Server Management section

## Preferences Integration

The `OnGUI()` method in `NeovimCodeEditor.cs` will be modified:

**Keep:**
- Analyzers section (add/remove/browse)
- .csproj generation toggles (Embedded, Local, Registry, Git, etc.)
- Regenerate project files button

**Add:**
- "Open Neovim Settings" button (opens NeovimSettingsWindow)

**Remove:**
- Behavior Settings section (moves to Neovim Settings window)
- Server Management section (moves to Neovim Settings window)

## Implementation Approach

1. **Create NeovimSettingsWindow.cs** with TabView structure
2. **Extract UI code** from each existing modal window's CreateGUI()
3. **Port to tabs** - adapt extracted code for tab context
4. **Add Preferences button** in NeovimCodeEditor.OnGUI()
5. **Remove migrated sections** from Preferences
6. **Delete obsolete files** - the 4 modal window files

## Data Flow

Settings continue to use the existing `NeovimEditorConfig` class with `EditorPrefs` storage:
- All tabs read from `NeovimCodeEditor.s_Config`
- Changes call `s_Config.Save()` immediately
- No data migration needed (storage unchanged)

## Success Criteria

- [ ] Window opens via `Window/Neovim` menu
- [ ] Window opens via "Open Neovim Settings" button in Preferences
- [ ] All 4 tabs display correctly with proper content
- [ ] Settings persist after closing window
- [ ] Old modal window files are deleted
- [ ] No "Neovim" top-level menu remains
- [ ] Analyzers and .csproj toggles remain functional in Preferences
