@echo off
setlocal
rem Run from any directory; locate the Visual Studio x86 toolchain if needed.
where cl >nul 2>nul
if errorlevel 1 (
    for /f "usebackq tokens=*" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do call "%%I\VC\Auxiliary\Build\vcvars32.bat"
)
where cl >nul 2>nul
if errorlevel 1 (
    echo Visual Studio C++ x86 build tools are required.
    exit /b 1
)
cmake -S "%~dp0." -B "%~dp0build_dc3pp_win32" -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 exit /b 1
cmake --build "%~dp0build_dc3pp_win32" --config Release
if errorlevel 1 exit /b 1
echo Built Da Capo 3 Platinum Partner patch and launcher:
echo   %~dp0build_dc3pp_win32\DC3PPPatch.dll
echo   %~dp0build_dc3pp_win32\DC3PPLauncher.exe
exit /b 0
