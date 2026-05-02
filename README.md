# DC3PP MES Text Tool (GUI + CLI)

A Windows tool for converting **Da Capo 3 Platinum Partner** script files between:

- `MES -> JSON` (export)
- `JSON -> MES` (import)

It supports both:

- Single file conversion
- Batch conversion (entire folder)

## Tool Location

Main executable:

- `bin\Release\net8.0-windows\DC3PPMesTextTool_GUI.exe`

RAR package:

- `bin\Release\net8.0-windows\DC3PPMesTextTool_GUI.rar`

## Output Build Locations (Patch + Launcher)

Patch and launcher build output:

- `DC3PPPatch a\build`

Ready-to-use release output:

- `DC3PPPatch a\build\Released`

Release patch archive:

- `DC3PPPatch a\build\Released\Released_patch.rar`

## Important: Custom Launcher Required

To use this patch correctly, launch the game with:

- `H:\Games\DC3PP\DC3PPMesTextTool_GUI and Patch\DC3PPPatch a\build\Released\DC3PPLauncher.exe`

Running the game directly from `DC3PP.EXE` may not load the patch properly.

## Features

- GUI mode (double-click executable)
- CLI mode (run commands from terminal)
- Drag-and-drop support for `.mes` and folders
- Shift-JIS handling for game script text

## JSON Format

Exported JSON is an array of entries like:

```json
[
  { "name": "Character Name", "message": "Dialogue line" },
  { "message": "Narration line" }
]
```

## Quick Start (GUI)

1. Open `DC3PPMesTextTool_GUI.exe`.
2. Use **Single File** tab for one file.
3. Use **Batch** tab for folder-based conversion.
4. Check the log panel for success/error details.

### Single File: Export (MES -> JSON)

1. In `Input MES/JSON`, select a `.mes` file.
2. In `Output JSON/MES`, set output `.json` path (optional).
3. Click `Export (MES -> JSON)`.

### Single File: Import (JSON -> MES)

Important behavior in current GUI:

- GUI auto-detects original MES by replacing `.json` with `.mes` in the same folder.
- If matching original `.mes` is not found, import will fail.

Steps:

1. In `Input MES/JSON`, select a `.json` file.
2. Make sure original `.mes` with the same base filename is in the same folder.
3. Set output `.mes` path (optional).
4. Set `Word Wrap Width` (default: `67`).
5. Click `Import (JSON -> MES)`.

## CLI Tutorial

Open terminal in:

- `bin\Release\net8.0-windows`

Use:

- `./DC3PPMesTextTool_GUI.exe <command> ...`

### 1) Export Single File (MES -> JSON)

```powershell
./DC3PPMesTextTool_GUI.exe export "H:\path\script.mes" "H:\path\script.json"
```

If output path is omitted, output defaults to same filename with `.json` extension.

### 2) Import Single File (JSON -> MES)

```powershell
./DC3PPMesTextTool_GUI.exe import "H:\path\script.json" "H:\path\script.mes" "H:\path\script_new.mes"
```

Notes:

- The second argument (`original.mes`) is required.
- If output path is omitted, output defaults to `output_<original_filename>.mes`.

### 3) Batch Export (Folder) (MES -> JSON)

```powershell
./DC3PPMesTextTool_GUI.exe batch-export "H:\path\mes_folder" "H:\path\json_output"
```

If output folder is omitted, it defaults to `mes_folder\json_output`.

### 4) Batch Import (Folder) (JSON -> MES)

```powershell
./DC3PPMesTextTool_GUI.exe batch-import "H:\path\json_folder" "H:\path\mes_folder" "H:\path\mes_output" -w 67
```

Arguments:

- `json_folder`: translated `.json` files
- `mes_folder`: original `.mes` files used as base
- `mes_output`: output folder for rebuilt `.mes`
- `-w`: wrap width (optional, default `67`)

Matching is done by filename:

- `scene01.json` uses `scene01.mes`

If original MES file is missing, that JSON file is skipped.

## Drag & Drop

- Drop a `.mes` file onto the EXE: auto export to `.json` with same filename.
- Drop a folder onto the EXE: auto batch export all `.mes` files in that folder.

## Troubleshooting

- `original MES not found`: ensure matching `.mes` exists for each `.json`.
- Import output looks broken: try different wrap width for import (`-w`).
- No output file: check write permission for output folder.

## Build & Release Summary

- Active build folder: `DC3PPPatch a\build`
- Final ready release folder: `DC3PPPatch a\build\Released`

## Release Description (for GitHub Releases)

```text
Initial public release.
Includes:
Released_patch.rar
DC3PPMesTextTool_GUI.rar
Important: Use the custom DC3PPLauncher from build/Released to run the patch properly.

The file doesn't contain any viruses; if your antivirus somehow detect it as anomaly.. it's just a false alarm. Trust me, I made this patch for everyone who wants to create or modify the Da Capo 3 Platinum Partner patch. I have no intention of harming anyone.
```
