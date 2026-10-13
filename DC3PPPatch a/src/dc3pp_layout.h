#pragma once
#include <intrin.h>

// Verified against DC3PP.EXE SHA256 af45da50b4aea1cded2465ba76f2e213af24e32d7366268f18943835f3097043.
// Native atlas: 116px cells, six columns, origin (168,132). IDs come from
// 0x47D3F8; translations were matched against the original MES speaker slots.
namespace dc3pp_layout {
struct Alias { const char* name; int icon; };
static constexpr Alias aliases[] = {
    {"Aoi",0}, {"Suara Aoi",0}, {"Sara",1}, {"Suara Sara",1},
    {"Himeno",2}, {"Suara Himeno",2}, {"Himeno Khayalan",2},
    {"Rikka",3}, {"Suara Rikka",3}, {"Charles",4}, {"Suara Charles",4},
    {"Suginami",5}, {"Suara Suginami",5}, {"Sumomo",6}, {"Suara Sumomo",6},
    {"Youko",7}, {"Kousuke",9}, {"Suara Kousuke",9}, {"Sakura",10},
    {"Mikoto",11}, {"Suara Mikoto",11}, {"Minatsu",12}, {"Suara Minatsu",12},
    {"Ayah Sara",13}, {"Ayahnya Sara",13}, {"Ibu Sara",14}, {"Ibunya Sara",14},
    {"Shiki",15}, {"Suara Shiki",15}, {"Yuuhi",16}, {"Suara Yuuhi",16},
    {"Yuzu",17}, {"Suara Yuzu",17}, {"Kiyotaka",18}, {"Kiyokata",18},
    {"Kiyotaka Khayalan",18}
};

// Exact match only: groups and unrecognised speakers retain native text fallback.
inline int LookupIcon(const char* text) {
    if (!text) return -1;
    char normalized[256];
    size_t n = 0;
    for (; text[n] && n < sizeof(normalized)-1; ++n)
        normalized[n] = text[n] == '_' ? ' ' : text[n];
    if (text[n]) return -1;
    normalized[n] = 0;
    for (const auto& a : aliases)
        if (strcmp(normalized, a.name) == 0) return a.icon;
    return -1;
}

inline bool HasRuby(const char* text) {
    if (!text) return false;
    const auto* p = reinterpret_cast<const unsigned char*>(text);
    while (*p) {
        if (*p == 0x81 && p[1] == 0x6F) return true;
        if ((*p >= 0x81 && *p <= 0x9F) || (*p >= 0xE0 && *p <= 0xFC)) {
            if (!p[1]) break;
            ++p;
        }
        ++p;
    }
    return false;
}
inline int AdjustPitch(int pitch, int spacing) {
    // Negative pitch means vertical Japanese text. Do not alter that mode.
    if (pitch < 24) return pitch;
    if (spacing < -10) spacing = -10;
    if (spacing > 64) spacing = 64;
    const int adjusted = pitch + spacing;
    return adjusted < 24 ? 24 : adjusted;
}
inline bool NeedsCompactPitch(const char* text) {
    if (!text || text[0] != '@' || text[1] != 's') return false;
    int lines = 1;
    for (const char* p=text; *p; ++p) if (*p == '\n') ++lines;
    return lines >= 5;
}
}

static int __cdecl Dc3ppTranslatedIcon(const char* name) {
    return g_fontManager.GetEnableBacklogAllIcon() ? dc3pp_layout::LookupIcon(name) : -1;
}
static DWORD dc3ppIconFound = 0x41031E;
static DWORD dc3ppIconFallback = 0x4103A3;
// Replaces CMP ESI,-1 / JZ fallback after the original Japanese lookup.
// Str1 is [ESP+28h]. PUSHFD/PUSHAD add 24h; saved ESI is [ESP+4].
static __declspec(naked) void Dc3ppIconHook() {
    __asm {
        cmp esi, -1
        jne found
        pushfd
        pushad
        lea eax, [esp+4Ch]
        push eax
        call Dc3ppTranslatedIcon
        add esp, 4
        mov [esp+4], eax
        popad
        popfd
        cmp esi, -1
        je fallback
    found:
        jmp dword ptr [dc3ppIconFound]
    fallback:
        jmp dword ptr [dc3ppIconFallback]
    }
}

static int (__cdecl* Dc3ppRealBody)(const char*, int) = (decltype(Dc3ppRealBody))0x423030;
static int (__cdecl* Dc3ppRealLive)() = (decltype(Dc3ppRealLive))0x42D9C0;
static int (__cdecl* Dc3ppRealRedraw)(int) = (decltype(Dc3ppRealRedraw))0x427BC0;
static int (__cdecl* Dc3ppRealReset)() = (decltype(Dc3ppRealReset))0x426500;
static thread_local int dc3ppPitchDepth = 0;
struct Dc3ppPitchScope;
static thread_local Dc3ppPitchScope* dc3ppPitchOwner = nullptr;
struct Dc3ppPitchScope {
    int saved;
    bool owner;
    int spacing;
    const char* text;
    __declspec(noinline) Dc3ppPitchScope(int spacing, const char* text) :
        saved(*reinterpret_cast<int*>(0x4881D8)), owner(dc3ppPitchDepth++ == 0),
        spacing(spacing), text(text) {
        if (owner) {
            dc3ppPitchOwner = this;
            Apply();
        }
    }
    void Apply() const {
        if (!g_fontManager.GetDisableBacklogSpacing() && !dc3pp_layout::HasRuby(text)) {
            *reinterpret_cast<int*>(0x4881D8) = dc3pp_layout::AdjustPitch(saved, spacing);
            // Five lines in the native 16px font fit a 100px body at 20px pitch.
            if (saved >= 24 && spacing < 0 && dc3pp_layout::NeedsCompactPitch(text))
                *reinterpret_cast<int*>(0x4881D8) = 20;
        }
    }
    ~Dc3ppPitchScope() {
        if (owner) {
            *reinterpret_cast<int*>(0x4881D8) = saved;
            dc3ppPitchOwner = nullptr;
        }
        --dc3ppPitchDepth;
    }
};
static int __cdecl Dc3ppReset() {
    const int result = Dc3ppRealReset();
    // Preparing/repainting the dialogue window resets the pitch from its layout
    // resource. Reapply the active scope after that reset, before any glyphs are
    // placed. Otherwise the first/complete redraw uses 34px and typing uses 24px.
    if (dc3ppPitchOwner) {
        dc3ppPitchOwner->saved = *reinterpret_cast<int*>(0x4881D8);
        dc3ppPitchOwner->Apply();
    }
    return result;
}
static int __cdecl Dc3ppBody(const char* text, int color) {
    const auto caller = reinterpret_cast<DWORD>(_ReturnAddress());
    // Only the horizontal backlog card's body call. Names and vertical log excluded.
    if (caller == 0x4104E4) {
        Dc3ppPitchScope scope(g_fontManager.GetBacklogLineSpacing(), text);
        return Dc3ppRealBody(text, color);
    }
    return Dc3ppRealBody(text, color);
}
static int __cdecl Dc3ppLive() {
    Dc3ppPitchScope scope(g_fontManager.GetDialogueLineSpacing(), reinterpret_cast<const char*>(0xBAB530));
    return Dc3ppRealLive();
}
static int __cdecl Dc3ppRedraw(int entry) {
    Dc3ppPitchScope scope(g_fontManager.GetDialogueLineSpacing(), reinterpret_cast<const char*>(0xBAB530));
    return Dc3ppRealRedraw(entry);
}

static bool InstallDc3ppLayout() {
    if (reinterpret_cast<DWORD>(GetModuleHandleW(nullptr)) != 0x400000) return false;
    const BYTE icon[] = {0x83,0xFE,0xFF,0x0F,0x84,0x85,0,0,0};
    const BYTE body[] = {0x53,0x8B,0x5C,0x24,0x08,0x55,0x56};
    const BYTE live[] = {0x83,0xEC,0x08,0x53,0x55,0x33,0xED};
    const BYTE redraw[] = {0x81,0xEC,0x08,0x05,0,0};
    const BYTE reset[] = {0x6A,0x0A,0xE8,0x79,0xB2,0xFD,0xFF};
    if (memcmp((void*)0x410315,icon,sizeof(icon)) || memcmp((void*)0x423030,body,sizeof(body)) ||
        memcmp((void*)0x42D9C0,live,sizeof(live)) || memcmp((void*)0x427BC0,redraw,sizeof(redraw)) ||
        memcmp((void*)0x426500,reset,sizeof(reset))) {
        OutputDebugStringA("DC3PP layout: executable signature mismatch; skipped.\n");
        return false;
    }
    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    LONG status = DetourAttach(&(PVOID&)Dc3ppRealBody,Dc3ppBody);
    if (!status) status = DetourAttach(&(PVOID&)Dc3ppRealLive,Dc3ppLive);
    if (!status) status = DetourAttach(&(PVOID&)Dc3ppRealRedraw,Dc3ppRedraw);
    if (!status) status = DetourAttach(&(PVOID&)Dc3ppRealReset,Dc3ppReset);
    if (status) { DetourTransactionAbort(); return false; }
    if (DetourTransactionCommit() != NO_ERROR) return false;
    DWORD old;
    if (!VirtualProtect((void*)0x410315,9,PAGE_EXECUTE_READWRITE,&old)) return false;
    BYTE patch[9] = {0xE9,0,0,0,0,0x90,0x90,0x90,0x90};
    const DWORD relative = reinterpret_cast<DWORD>(Dc3ppIconHook)-0x410315-5;
    memcpy(patch+1,&relative,4);
    memcpy((void*)0x410315,patch,9);
    VirtualProtect((void*)0x410315,9,old,&old);
    FlushInstructionCache(GetCurrentProcess(),(void*)0x410315,9);
    OutputDebugStringA("DC3PP layout: speaker icons and native line pitch installed.\n");
    return true;
}
