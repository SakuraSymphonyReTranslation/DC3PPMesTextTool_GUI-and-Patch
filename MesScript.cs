using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DC3PPMesTextTool;

/// <summary>
/// MES script parser and text extractor.
/// </summary>
public class MesScript
{
    private byte[] _rawData = Array.Empty<byte>();
    private ScriptInfo _info = ScriptInfo.DC3PP;
    private List<Token> _tokens = new();
    private int _labelCount;
    private int _bytecodeOffset;
    private int[] _labels = Array.Empty<int>();
    
    // Regex pattern for detecting choice text
    private static readonly Regex ChoicePattern = new(@"戟ｅ訓.*|ワ[一-龯]", RegexOptions.Compiled);
    
    public byte[] RawData => _rawData;
    public ScriptInfo Info => _info;
    public IReadOnlyList<Token> Tokens => _tokens;
    public int LabelCount => _labelCount;
    public int BytecodeOffset => _bytecodeOffset;
    
    public void Load(byte[] data, ScriptInfo? info = null)
    {
        _rawData = data;
        if (info != null && !ReferenceEquals(info, ScriptInfo.DC3PP))
            throw new InvalidDataException("Only Da Capo 3 Platinum Partner MES files are supported.");
        _info = ScriptInfo.QueryFromData(data)
            ?? throw new InvalidDataException("Unsupported MES format. This tool supports Da Capo 3 Platinum Partner only.");
        
        ParseStructure();
        TokenizeBytecode();
    }
    
    public void Load(string path, ScriptInfo? info = null)
    {
        Load(File.ReadAllBytes(path), info);
    }
    
    private void ParseStructure()
    {
        if (_rawData.Length < 8)
            throw new InvalidDataException("MES file too small");
            
        int rawCount = BitConverter.ToInt32(_rawData, 0);
        
        // Calculate bytecode offset based on offset type
        _bytecodeOffset = (int)_info.OffsetType switch
        {
            (int)OffsetType.Offset1 => rawCount * 4 + 4,
            (int)OffsetType.Offset2 => rawCount * 6 + 4,
            _ => rawCount * 4 + 4
        };
        
        if (_bytecodeOffset >= _rawData.Length)
            throw new InvalidDataException("Invalid MES structure");
            
        // Calculate ACTUAL number of labels based on available header space
        // DC3PP labels start at 8.
        int labelStartOffset = 4;
        if ((int)_info.OffsetType == (int)OffsetType.Offset2)
        {
            labelStartOffset = 8;
        }
        else if ((int)_info.OffsetType == (int)OffsetType.Offset1 && BitConverter.ToInt32(_rawData, 4) == 3)
        {
            labelStartOffset = 8;
        }

        // Calculate stride
        int stride = 4;
        
        // Use explicit count from header
        _labelCount = rawCount;
        
        _labels = new int[_labelCount];
        for (int i = 0; i < _labelCount; i++)
        {
            _labels[i] = BitConverter.ToInt32(_rawData, labelStartOffset + i * stride);
        }
    }
    
    private int CalcVersionLength()
    {
        // Check if header format implies version length
        if ((int)_info.OffsetType == (int)OffsetType.Offset2)
        {
            return 3; // DC3PP always uses 3 bytes (Version + Padding)
        }
        else if ((int)_info.OffsetType == (int)OffsetType.Offset1)
        {
             // If header[1] == 3, version len is 3
             // But simpler to rely on labels[0] or raw check
             if (BitConverter.ToInt32(_rawData, 4) == 3) return 3;
        }
        return (_info.Version & 0xFF00) == 0 ? 1 : 2;
    }

    private void TokenizeBytecode()
    {
        _tokens.Clear();
        // Start after the version token
        int pos = _bytecodeOffset + CalcVersionLength();
        
        while (pos < _rawData.Length)
        {
            byte opcode = _rawData[pos];
            int tokenLength;
            
            // Determine token length based on opcode type
            if (_info.IsInSection(_info.Uint8x2, opcode))
            {
                // [op][arg1][arg2] = 3 bytes
                tokenLength = 3;
            }
            else if (_info.IsInSection(_info.Uint8Str, opcode))
            {
                // [op][arg1][string\0]
                tokenLength = 2 + GetStringLength(pos + 2) + 1;
            }
            else if (_info.IsInSection(_info.String, opcode))
            {
                // [op][string\0]
                tokenLength = 1 + GetStringLength(pos + 1) + 1;
            }
            else if (_info.IsInSection(_info.EncStr, opcode))
            {
                // [op][encstring\0]
                tokenLength = 1 + GetStringLength(pos + 1) + 1;
            }
            else if (_info.IsInSection(_info.Uint16x4, opcode))
            {
                // [op][4 x uint16] = 9 bytes
                tokenLength = 9;
            }
            else
            {
                // Unknown opcode, skip 1 byte
                tokenLength = 1;
            }
            
            // Bounds check
            if (pos + tokenLength > _rawData.Length)
                break;
                
            // Tokens are relative to the start of ACTUAL bytecode (after version token)
            _tokens.Add(new Token(opcode, pos - (_bytecodeOffset + CalcVersionLength()), tokenLength));
            pos += tokenLength;
        }
    }
    
    private int GetStringLength(int offset)
    {
        int length = 0;
        while (offset + length < _rawData.Length && _rawData[offset + length] != 0)
        {
            length++;
        }
        return length;
    }
    
    public List<TextEntry> ExtractText(Encoding encoding, bool verbose = false)
    {
        var entries = new List<TextEntry>();
        int emptyCount = 0;
        
        foreach (var token in _tokens)
        {
            string? text = null;
            bool isEncrypted = false;
            
            if (_info.IsInSection(_info.EncStr, token.Opcode))
            {
                text = DecryptString(token.Offset + 1, encoding);
                isEncrypted = true;
            }
            else if (_info.OutputOpcodes.Contains(token.Opcode))
            {
                text = ReadString(token.Offset + 1, encoding);
            }
            
            if (text == null) continue;
            
            if (string.IsNullOrEmpty(text))
            {
                emptyCount++;
                continue;
            }
                
            text = CleanText(text);
            if (isEncrypted) text = text.Replace('_', ' ');
            
            if (string.IsNullOrEmpty(text))
            {
                emptyCount++;
                continue;
            }
            
            bool isChoice = ChoicePattern.IsMatch(text);
            
            var entry = new TextEntry
            {
                Offset = token.Offset,
                OriginalLength = token.Length,
                Opcode = token.Opcode,
                IsEncrypted = isEncrypted,
                Message = text,
                IsChoice = isChoice
            };
            
            entries.Add(entry);
        }
        
        return entries;
    }
    
    public List<TextEntry> ExtractTextPatternBased(Encoding encoding)
    {
        // Fallback for when opcode extraction fails.
        // For DC3PP, strict opcode extraction is preferred.
        // Returning empty list to satisfy build; if opcode extraction fails, we have bigger issues.
        return new List<TextEntry>();
    }

    public byte[] ImportText(List<TextEntry> translatedEntries, Encoding encoding, TextFormatter? formatter = null,
        Action<LayoutWarning>? reportWarning = null)
    {
         // Re-extract original text to get offsets
        var originalEntries = ExtractText(encoding, false).ToList();
        
        if (originalEntries.Count != translatedEntries.Count)
            throw new InvalidDataException($"Slot count mismatch: MES has {originalEntries.Count}, JSON has {translatedEntries.Count}.");
        encoding = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var translationMap = new Dictionary<int, string>();
        for (int i = 0; i < originalEntries.Count; i++)
        {
            var origEntry = originalEntries[i];
            var text = translatedEntries[i].Message;
            
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
                throw new InvalidDataException($"MES slot {i + 1}: text must be nonempty and contain no NUL.");
            string normalized = TextFormatter.NormalizeForEngine(text);
            if (normalized != text)
                reportWarning?.Invoke(new(i + 1, "Normalized line endings or unsupported CP932 punctuation/accent.", normalized));
            text = normalized;
            bool body = origEntry.Opcode == 0x50 && !origEntry.IsChoice;
            if (formatter != null && body)
            {
                text = formatter.FormatBody(text);
            }
            if (origEntry.Opcode == 0x4F && text.Contains('\n'))
                throw new InvalidDataException($"MES slot {i + 1}: speaker names cannot contain newlines.");
            try { _ = encoding.GetBytes(text); }
            catch (EncoderFallbackException ex) { throw new InvalidDataException($"MES slot {i + 1}: text cannot be encoded as CP{encoding.CodePage}.", ex); }
            if (body && text.Split('\n').Length > TextFormatter.RecommendedLines)
                reportWarning?.Invoke(new(i + 1, $"{text.Split('\n').Length} lines; backlog card recommends at most 4.", text));
            if (body && TextFormatter.RequiresNativeLayout(text) && !text.StartsWith("@s"))
                reportWarning?.Invoke(new(i + 1, "Native layout retained for ruby/font controls; inspect in game.", text));
            
            translationMap[origEntry.Offset] = text;
        }
        
        int versionLen = CalcVersionLength();
        
        // Clone raw data for the header
        byte[] newHeader = new byte[_bytecodeOffset];
        Array.Copy(_rawData, newHeader, _bytecodeOffset);
        
        int labelStartOffset = 4;
        if ((int)_info.OffsetType == (int)OffsetType.Offset2) labelStartOffset = 8;
        else if ((int)_info.OffsetType == (int)OffsetType.Offset1 && BitConverter.ToInt32(_rawData, 4) == 3) labelStartOffset = 8;
        
        // Note: _labelCount is now derived from available space
        
        // Determine stride
        int stride = 4;
        
        using var bytecodeOutput = new MemoryStream();
        int labelIndex = 0;

        foreach (var token in _tokens)
        {
            long currentPos = bytecodeOutput.Position; // Position relative to start of bytecode
            
            // C++ Logic: "Update label if we see Opcode 0x03 or 0x04" (for Offset2/DC3PP)
            if ((int)_info.OffsetType == (int)OffsetType.Offset2)
            {
                if (token.Opcode == 0x03 || token.Opcode == 0x04)
                {
                    if (labelIndex < _labelCount)
                    {
                        // Calculate New Relative Offset 
                        // DC3PP uses offsets relative to the start of the bytecode section
                        // Offset2 labels are relative to the start of the
                        // version token, while currentPos is relative to the
                        // executable bytecode after that token.
                        int newRelative = (int)currentPos + versionLen + token.Length;
                        
                        // Read valid original label to preserve high bits (masks)
                        int oldLabel = BitConverter.ToInt32(newHeader, labelStartOffset + labelIndex * stride);
                        
                        // SAFETY: Only update if the old label looks like a valid pointer (in-bounds)
                        // DC3PP pointers are relative, so we check if it's < rawData length
                        int oldOffset = oldLabel & 0x00FFFFFF;
                        if (oldOffset < _rawData.Length) 
                        {
                            int newLabelValue = (oldLabel & unchecked((int)0xFF000000)) | (newRelative & 0x00FFFFFF);
                            byte[] labelBytes = BitConverter.GetBytes(newLabelValue);
                            Array.Copy(labelBytes, 0, newHeader, labelStartOffset + labelIndex * stride, 4);
                        }
                        
                        labelIndex++;
                    }
                }
            }
            else
            {
                 // Fallback for the generic Offset1 layout
                 int tokenAbsStart = _bytecodeOffset + versionLen + token.Offset;
                 int tokenAbsEnd = tokenAbsStart + token.Length;
                 
                 for(int i=0; i<_labelCount; i++)
                 {
                     int oldLabel = BitConverter.ToInt32(newHeader, labelStartOffset + i * stride);
                     int rawLabel = oldLabel & 0x7FFFFFFF;
                     if (rawLabel == tokenAbsEnd)
                     {
                         // SAFETY CHECK
                         int oldOffset = oldLabel & 0x00FFFFFF;
                         if (oldOffset < _rawData.Length)
                         {
                             int newAbsolute = _bytecodeOffset + versionLen + (int)currentPos + GetNewTokenLength(token, translationMap, encoding);
                             int newLabelValue = (oldLabel & unchecked((int)0xFF000000)) | (newAbsolute & 0x00FFFFFF);
                             byte[] labelBytes = BitConverter.GetBytes(newLabelValue);
                             Array.Copy(labelBytes, 0, newHeader, labelStartOffset + i * stride, 4);
                         }
                     }
                 }
            }

            // Write Token
            bool isEncStr = _info.IsInSection(_info.EncStr, token.Opcode);
            bool isOutputOp = _info.OutputOpcodes.Contains(token.Opcode);
            
            if ((isEncStr || isOutputOp) && translationMap.TryGetValue(token.Offset, out var text))
            {
                // CRITICAL FIX: The engine uses null-terminated strings for encrypted text.
                // Encryption formula: ciphertext = plaintext - 0x20.
                // If plaintext contains an ASCII space (0x20), the ciphertext becomes 0x00.
                // This causes the game to prematurely terminate string reading, causing broken text.
                // We use an underscore ('_', 0x5F) as a temporary workaround 
                // because it is 1 byte, has a valid width in older engines, and does not encrypt to 0x00. 
                // DC3PP intercepts this to draw a normal space.
                if (isEncStr && text.Contains(' '))
                {
                    text = text.Replace(' ', '_');
                }

                byte[] textBytes = encoding.GetBytes(text);
                if (isEncStr)
                {
                    for (int i = 0; i < textBytes.Length; i++)
                    {
                        textBytes[i] = (byte)((textBytes[i] - _info.EncryptionKey) & 0xFF);
                    }
                }
                if (textBytes.Contains((byte)0))
                    throw new InvalidDataException($"Text at MES offset {token.Offset:X} contains an encoded terminator.");
                bytecodeOutput.WriteByte(token.Opcode);
                bytecodeOutput.Write(textBytes, 0, textBytes.Length);
                bytecodeOutput.WriteByte(0);
            }
            else
            {
                int tokenAbsoluteOffset = _bytecodeOffset + versionLen + token.Offset;
                if (!WriteNormalizedDc3PpSpriteCommand(bytecodeOutput, token, tokenAbsoluteOffset))
                {
                    bytecodeOutput.Write(_rawData, tokenAbsoluteOffset, token.Length);
                }
            }
        }
        
        // Construct Final File
        using var output = new MemoryStream();
        
        output.Write(newHeader, 0, newHeader.Length);
        output.Write(_rawData, _bytecodeOffset, versionLen);
        bytecodeOutput.Position = 0;
        bytecodeOutput.CopyTo(output);
        
        return output.ToArray();
    }

    /// <summary>
    /// DC3PP uses '?' as a runtime-selected size suffix in Aoi's BST commands.
    /// The shipped Aoi container is AOI_N1_2.CRM and its internal entries also
    /// use suffix "_2". On some configurations the engine resolves '?' to "1",
    /// causing the command to be shown as text and the layered sprite to break.
    /// Resolve only Aoi's BST wildcard here; the replacement is byte-for-byte
    /// the same length, so branch labels and all following token offsets stay valid.
    /// </summary>
    private bool WriteNormalizedDc3PpSpriteCommand(Stream output, Token token, int tokenAbsoluteOffset)
    {
        if (!ReferenceEquals(_info, ScriptInfo.DC3PP) || token.Opcode != 0x2C || token.Length < 8)
        {
            return false;
        }

        // Opcode 0x2C is [op][arg1][string\0].
        int stringOffset = tokenAbsoluteOffset + 2;
        int stringLength = token.Length - 3;
        ReadOnlySpan<byte> command = _rawData.AsSpan(stringOffset, stringLength);
        ReadOnlySpan<byte> aoiPrefix = "BST\\AOI_"u8;

        if (!command.StartsWith(aoiPrefix) || command.IndexOf((byte)'?') < 0)
        {
            return false;
        }

        byte[] normalizedToken = _rawData.AsSpan(tokenAbsoluteOffset, token.Length).ToArray();
        for (int i = 2; i < normalizedToken.Length - 1; i++)
        {
            if (normalizedToken[i] == (byte)'?')
            {
                normalizedToken[i] = (byte)'2';
            }
        }

        output.Write(normalizedToken, 0, normalizedToken.Length);
        return true;
    }

    private int GetNewTokenLength(Token token, Dictionary<int, string> translationMap, Encoding encoding)
    {
        bool isEncStr = _info.IsInSection(_info.EncStr, token.Opcode);
        bool isOutputOp = _info.OutputOpcodes.Contains(token.Opcode);
        
        if ((isEncStr || isOutputOp) && translationMap.TryGetValue(token.Offset, out var text))
        {
            byte[] textBytes = encoding.GetBytes(text);
            return 1 + textBytes.Length + 1; // opcode + text + null
        }
        return token.Length;
    }
    
    private string DecryptString(int relativeOffset, Encoding encoding)
    {
        int absoluteOffset = _bytecodeOffset + CalcVersionLength() + relativeOffset;
        var bytes = new List<byte>();
        
        while (absoluteOffset < _rawData.Length && _rawData[absoluteOffset] != 0)
        {
            byte decrypted = (byte)((_rawData[absoluteOffset] + _info.EncryptionKey) & 0xFF);
            bytes.Add(decrypted);
            absoluteOffset++;
        }
        
        return encoding.GetString(bytes.ToArray());
    }
    
    private string ReadString(int relativeOffset, Encoding encoding)
    {
        int absoluteOffset = _bytecodeOffset + CalcVersionLength() + relativeOffset;
        var bytes = new List<byte>();
        
        while (absoluteOffset < _rawData.Length && _rawData[absoluteOffset] != 0)
        {
            bytes.Add(_rawData[absoluteOffset]);
            absoluteOffset++;
        }
        
        return encoding.GetString(bytes.ToArray());
    }
    
    private static string CleanText(string text)
    {
        text = text.TrimStart('$');  
        text = text.TrimEnd('$', '　', ' '); 
        text = text.Replace("$", " ");
        return text;
    }
}

// Token, TextEntry, TextFormatter are defined in separate files.
