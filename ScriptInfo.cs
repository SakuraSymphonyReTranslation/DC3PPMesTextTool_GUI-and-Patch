namespace DC3PPMesTextTool;

/// <summary>
/// Script configuration for Da Capo 3 Platinum Partner.
/// Based on MesTextTool's script_info.cpp
/// </summary>
public class ScriptInfo
{
    public string Name { get; init; } = "";
    public ushort Version { get; init; }
    public OffsetType OffsetType { get; init; }
    
    // Opcode ranges for different instruction types
    public (byte Begin, byte End) Uint8x2 { get; init; }   // [op][arg1:byte][arg2:byte]
    public (byte Begin, byte End) Uint8Str { get; init; }  // [op][arg1:byte][string]
    public (byte Begin, byte End) String { get; init; }    // [op][string] - unencrypted
    public (byte Begin, byte End) EncStr { get; init; }    // [op][encstring] - encrypted
    public (byte Begin, byte End) Uint16x4 { get; init; }  // [op][4 x uint16]
    
    public byte EncryptionKey { get; init; }
    public byte[] OutputOpcodes { get; init; } = Array.Empty<byte>();
    
    /// <summary>
    /// Check if an opcode falls within a section range
    /// </summary>
    public bool IsInSection((byte Begin, byte End) section, byte opcode)
    {
        if (section.Begin == 0xFF && section.End == 0xFF)
            return false;
        return opcode >= section.Begin && opcode <= section.End;
    }
    
    /// <summary>
    /// D.C.III Platinum Partner configuration
    /// </summary>
    public static readonly ScriptInfo DC3PP = new()
    {
        Name = "dc3pp",
        Version = 0x9872,
        OffsetType = OffsetType.Offset2,
        Uint8x2 = (0x00, 0x2A),
        Uint8Str = (0x2B, 0x32),
        String = (0x33, 0x4E),
        EncStr = (0x4F, 0x51),
        Uint16x4 = (0x52, 0xFF),
        EncryptionKey = 0x20,
        OutputOpcodes = new byte[] { 0x45 }
    };

    /// <summary>
    /// Supported script configuration
    /// </summary>
    public static readonly ScriptInfo[] AllInfos = { DC3PP };
    
    /// <summary>
    /// Query script info by name
    /// </summary>
    public static ScriptInfo? QueryByName(string name)
    {
        return AllInfos.FirstOrDefault(i => 
            i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Query script info from MES file data
    /// </summary>
    public static ScriptInfo? QueryFromData(byte[] data)
    {
        if (data.Length < 8 || BitConverter.ToInt32(data, 4) != 3)
            return null;

        int labelCount = BitConverter.ToInt32(data, 0);
        long versionOffset = (long)labelCount * 6 + 4;
        if (labelCount < 0 || versionOffset + 3 > data.Length)
            return null;

        return BitConverter.ToUInt16(data, (int)versionOffset) == DC3PP.Version
            ? DC3PP : null;
    }
}

public enum OffsetType
{
    Offset1,  // head[0] * 4 + 4
    Offset2   // head[0] * 6 + 4
}
