# DC4 Indonesian Font Patch - Setup Guide

## What We Created

I've created a complete DC4 font patch source code at `d:\vns\DC4\DC4Patch\`:

```
DC4Patch/
├── CMakeLists.txt          # Build configuration
├── src/
│   └── dc4patch.cpp        # Main DLL source code
├── Detours/                # Microsoft Detours library (not built)
```

The patch hooks Windows APIs to enable Indonesian character display:
- `GetGlyphOutlineA` - Replaces SJIS font with Unicode font
- `CreateFileA` - Redirects to `id_Data/` folder for translations
- `FindFirstFileA` - Supports file replacement

## Problem: No C++ Compiler

Your system doesn't have a C++ compiler installed:
- ❌ Visual Studio - Not found
- ❌ MinGW/GCC - Not found
- ❌ Clang - Not found

## Options to Proceed

### Option 1: Install Visual Studio Build Tools (Recommended)
Download and install from: https://visualstudio.microsoft.com/visual-cpp-build-tools/

After installing:
1. Open "Developer Command Prompt for VS"
2. Navigate to `d:\vns\DC4\DC4Patch`
3. Run:
```batch
cd Detours\src
nmake
cd ..\..
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A Win32
cmake --build . --config Release
```
4. Copy `build\Release\dinput8.dll` to `d:\vns\DC4\`

### Option 2: Install MinGW-w64
Download from: https://www.mingw-w64.org/downloads/

Then build with:
```batch
mkdir build && cd build
cmake .. -G "MinGW Makefiles"
cmake --build .
```

### Option 3: Contact the CircusEnginePatchs Author
You could reach out to the DC3WY patch author (cokkeijigen) on GitHub to request a DC4 version:
https://github.com/cokkeijigen/CircusEnginePatchs

### Option 4: Simple Workaround with Locale Emulator
For Indonesian (Latin only), you might be able to use:
1. Install [Locale Emulator](https://github.com/xupefei/Locale-Emulator)
2. Run DC4.EXE with Japanese locale
3. The Latin characters might display with certain fonts

---

## Once You Have the DLL

After building `dinput8.dll`:

1. Copy `dinput8.dll` to `d:\vns\DC4\` (same folder as DC4.EXE)
2. Create folder `d:\vns\DC4\id_Data\`
3. Place your translated `.mes` files in `id_Data/`
4. Run DC4.EXE normally - the patch loads automatically

## Testing

To verify the patch is working:
- Check if the window title changes
- Try displaying Indonesian text in-game
- Use right-click menu to change font if needed
