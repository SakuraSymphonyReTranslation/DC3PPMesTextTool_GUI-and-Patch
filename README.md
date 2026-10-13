# Da Capo 3 Platinum Partner — MES Text Tool & Patch

A Windows MES/JSON converter and x86 runtime patch for **Da Capo 3 Platinum Partner (DC3PP)**. The converter supports this game only, with a GUI, command-line commands, single-file conversion, and batch conversion.

## Download

Get both updated packages from [release v1.0.0](https://github.com/SakuraSymphonyReTranslation/DC3PPMesTextTool_GUI-and-Patch/releases/tag/v1.0.0):

| Package | Purpose |
| --- | --- |
| `Released_patch.rar` | Game runtime patch, custom launcher, configuration, and supporting files |
| `DC3PPMesTextTool_GUI.rar` | MES ↔ JSON converter for editing and rebuilding scripts |

The converter requires the **.NET 8 Desktop Runtime for Windows**. Install the runtime matching your Windows architecture from [Microsoft's .NET 8 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). The runtime patch requires an existing DC3PP game installation. Translation scripts and artwork are supplied separately.

## Game folder and installation

**`<GAME_DIR>` means your game installation folder: the folder containing `DC3PP.EXE`.** It can be on any drive and can have any folder name. Replace this placeholder with your own location; do not create a folder literally named `<GAME_DIR>`.

1. Close the game. Back up your existing `DC3PP.EXE`, patch files, configuration, and saves.
2. Extract the contents of `Released_patch.rar` directly into `<GAME_DIR>`. The package includes the locally supplied `DC3PP.EXE`, so extraction replaces that file when you accept overwrite.
3. Place your translated MES files in `<GAME_DIR>\id_Data\MES` for Indonesian or `<GAME_DIR>\eng_data\MES` for English. Keep the original filenames.
4. Set `Language` under `[Settings]` in `DC3PPPatch.ini`: `0` = Japanese, `1` = Indonesian, `2` = English. The included configuration selects Indonesian and enables file redirection.
5. Launch **`<GAME_DIR>\DC3PPLauncher.exe`**. Starting `DC3PP.EXE` directly may leave the patch unloaded.

Example layout after installing the patch and adding your translation files:

```text
<GAME_DIR>\
├── DC3PP.EXE
├── DC3PPLauncher.exe
├── DC3PPPatch.dll
├── DC3PPPatch.ini
├── LoaderDll.dll
├── LocaleEmulator.dll
├── scenario_titles.ini
├── AdvData\
├── id_Data\MES\       (your Indonesian scripts)
└── eng_data\MES\      (your English scripts)
```

Extract `DC3PPMesTextTool_GUI.rar` into a separate tools folder and keep its executable, DLL, and JSON runtime files together. Open `DC3PPMesTextTool_GUI.exe` to use the GUI.

## New word wrap and native layout

The default **Word Wrap Width is `46` half-width columns**, chosen for the dialogue window and the narrower backlog panel. This is a display-width budget: ASCII and half-width kana count as one column, while full-width characters count as two.

- Wrapping runs when **importing JSON into MES**. Exporting JSON or replacing the runtime DLL alone does not rewrap existing scripts; rebuild translated MES files with this converter to apply the new wrapping.
- Only dialogue and narration bodies are wrapped. Speaker names and choices keep their own layout.
- The wrapper prefers word boundaries and avoids leaving opening punctuation at the end of a line or closing punctuation at the beginning of a line. An overlong unbroken word can be split to fit.
- Manual line breaks are preserved. Existing native ruby/font/effect controls retain native layout instead of being automatically split. Supported timing commands do not consume visible columns.
- If ordinary dialogue would exceed four wrapped lines, the converter adds the game's native `@s` small-font control and wraps again at the equivalent physical width. Especially long passages can still need manual editing.
- Import checks text-slot counts and CP932 encoding before writing MES. A neighboring `filename.mes.layout-report` records layout and character-normalization notices to review.
- JSON uses readable spaces; import restores the game's storage conventions. Speaker names and dialogue are paired from MES opcodes, including translated dialogue without Japanese quotation marks.

The **converter sets line breaks**. `DC3PPPatch.dll` separately handles dialogue/backlog line spacing, translated speaker face icons, and layout reset between dialogue transitions. Keep the included configuration as a starting point; a different font or font size may require another wrap width. The supported width range is `10`–`100`.

## JSON format

```json
[
  { "name": "Character Name", "message": "Dialogue line" },
  { "message": "Narration line" }
]
```

Keep the entry order and text slots from the export. The original MES is required when rebuilding a script.

## GUI quick start

### Export MES → JSON

1. Open the **Single File** tab.
2. Select a `.mes` file in **Input MES/JSON**.
3. Set **Output JSON/MES**, or leave it empty to use the input filename with a `.json` extension.
4. Click **Export (MES -> JSON)** and check the log.

### Import JSON → MES

1. Keep the original `.mes` beside the translated `.json`, with the same base filename. The GUI finds the original MES using that filename.
2. Select the `.json` in **Input MES/JSON**.
3. Choose a separate output `.mes` path to preserve the original.
4. Keep **Word Wrap Width** at `46` unless your font setup needs another value.
5. Click **Import (JSON -> MES)**, check the log, and review the `.layout-report`.

For batch export, select the **MES Folder** in the **Batch** tab. For batch import, select the translated **JSON Folder**, original **MES Folder**, and a separate **Output Folder**. Files are matched by base filename: `scene01.json` uses `scene01.mes`.

## Command line

Open PowerShell in the extracted converter folder. These examples use relative working folders; replace them with your own paths.

```powershell
# Create working folders, then place your original MES files in original
New-Item -ItemType Directory -Force .\original, .\translated, .\rebuilt | Out-Null

# Export one script
.\DC3PPMesTextTool_GUI.exe export ".\original\scene01.mes" ".\translated\scene01.json"

# Import one script (default wrap width: 46)
.\DC3PPMesTextTool_GUI.exe import ".\translated\scene01.json" ".\original\scene01.mes" ".\rebuilt\scene01.mes"

# Export a folder
.\DC3PPMesTextTool_GUI.exe batch-export ".\original" ".\translated"

# Import a folder with an explicit wrap width
.\DC3PPMesTextTool_GUI.exe batch-import ".\translated" ".\original" ".\rebuilt" -w 46
```

Output defaults when omitted:

| Command | Default output |
| --- | --- |
| `export` | Input filename with `.json` extension |
| `import` | `output_<original_filename>.mes` beside the original MES |
| `batch-export` | `json_output` inside the input MES folder |
| `batch-import` | `mes_output` inside the original MES folder |

`-w` is supported by `batch-import`; single-file CLI import uses the default width. Use the GUI to choose a different width for a single file. Missing originals or conversion failures cause a nonzero exit status.

Drag a `.mes` file onto the converter EXE to export it. Drag a folder containing MES files onto it to batch-export into `json_output`.

## Troubleshooting

- **Original MES not found:** provide the matching original file. For single-file GUI import, it must be beside the JSON.
- **Text-slot count or CP932 error:** restore the exported entry structure or correct the reported unsupported characters before importing again.
- **Text still wraps like the old version:** rebuild the translated MES with the new converter, then install the rebuilt files in the selected language's `MES` folder.
- **Dialogue/backlog layout is wrong:** verify that you launched `DC3PPLauncher.exe`, use the latest DLL and included configuration, and inspect the generated layout report. Check unusually long text in-game after rebuilding it.
- **Converter will not start:** install the .NET 8 Desktop Runtime and keep all converter runtime files together.

## Build from source

Converter: install the .NET 8 SDK, then run from the repository root:

```powershell
dotnet build .\DC3PPMesTextTool_GUI.csproj -c Release
```

Output: `bin\Release\net8.0-windows\DC3PPMesTextTool_GUI.exe` and its neighboring runtime files.

Native patch: install Visual Studio C++ x86 build tools and CMake, then run:

```powershell
& ".\DC3PPPatch a\build.bat"
```

Outputs: `DC3PPPatch a\build_dc3pp_win32\DC3PPPatch.dll` and `DC3PPLauncher.exe`. Copy both beside the game executable, together with `DC3PPPatch a\DC3PPPatch.ini` and `DC3PPPatch a\scenario_titles.ini`. The launcher embeds the required Locale Emulator DLLs.

Runtime source: [`dc3pppatch.cpp`](DC3PPPatch%20a/src/dc3pppatch.cpp). Native icons, line spacing, and transition reset: [`dc3pp_layout.h`](DC3PPPatch%20a/src/dc3pp_layout.h). See the [patch README](DC3PPPatch%20a/README.md) for more details.

Generated build folders are kept out of source control. Ready-to-use binaries are available in Releases.

## Credits and licenses

The converter derives from [MesTextTool](https://github.com/cokkeijigen/MesTextTool) by cokkeijigen. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The bundled Microsoft Detours and Locale Emulator sources retain their own license notices.
