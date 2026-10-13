# Da Capo 3 Platinum Partner Patch

This x86 runtime patch is loaded into `DC3PP.EXE` by `DC3PPLauncher.exe`.

## Build

Install Visual Studio C++ x86 build tools and CMake, then run `build.bat`.
Outputs are `build_dc3pp_win32/DC3PPPatch.dll` and `build_dc3pp_win32/DC3PPLauncher.exe`.

## Install and run

`<GAME_DIR>` means the player's installation folder containing `DC3PP.EXE`, on any drive.
Close the game and copy both outputs, `DC3PPPatch.ini`, and `scenario_titles.ini` into `<GAME_DIR>`, then run `<GAME_DIR>\DC3PPLauncher.exe`.
Settings are read from `DC3PPPatch.ini` beside the game executable. Translation overrides use `<GAME_DIR>\id_Data\MES` and `<GAME_DIR>\eng_data\MES`.
The included configuration selects Indonesian (`Language=1`); use `0` for Japanese or `2` for English.
The launcher embeds the required Locale Emulator DLLs.

The converter applies the new 46-column word wrap when rebuilding MES files. Updating the DLL alone does not rewrap existing scripts. The DLL handles native dialogue/backlog line spacing, speaker icons, and transition reset. See the parent README for the complete release installation instructions.

## Source

- `src/dc3pppatch.cpp`: fonts, encoding, translation overrides, and runtime initialization.
- `src/dc3pp_layout.h`: DC3PP speaker icons, dialogue/backlog line pitch, and transition reset hook.
- `src/launcher.cpp`: game startup, Japanese locale, and patch loading.
- `scenario_titles.ini`: DC3PP scenario title translations.

The native layout installer checks executable signatures before installing hooks.
See the parent README for the MES/JSON converter and current wrap settings.
