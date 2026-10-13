cmake_minimum_required(VERSION 3.15)
project(DC3PPPatch)

set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
set(CMAKE_CXX_STANDARD 20)

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "Error: Building for x64, but x86 expected. Use x86 Native Tools Command Prompt.")
endif()


# Detours library
set(DETOURS_DIR "${CMAKE_SOURCE_DIR}/Detours/src")
include_directories(${DETOURS_DIR})

add_library(detours STATIC
    ${DETOURS_DIR}/detours.cpp
    ${DETOURS_DIR}/modules.cpp
    ${DETOURS_DIR}/disasm.cpp
    ${DETOURS_DIR}/image.cpp
    ${DETOURS_DIR}/creatwth.cpp
    ${DETOURS_DIR}/disolx86.cpp
)

# ============================================================================
# DC3PPPatch.dll - The actual patch (injected by launcher)
# ============================================================================
add_library(DC3PPPatch SHARED
    src/dc3pppatch.cpp
)

target_link_libraries(DC3PPPatch PRIVATE
    detours
    gdi32
    user32
    advapi32
    comdlg32
)

set_target_properties(DC3PPPatch PROPERTIES
    LINK_FLAGS "/DYNAMICBASE:NO /MAP"
)


# ============================================================================
# DC3PPLauncher.exe - Starts the game with Japanese locale and injects DC3PPPatch.dll
# ============================================================================
add_executable(DC3PPLauncher WIN32
    src/launcher.cpp
    src/launcher.rc
)

target_include_directories(DC3PPLauncher PRIVATE src)

target_link_libraries(DC3PPLauncher PRIVATE
    user32
    advapi32
)
