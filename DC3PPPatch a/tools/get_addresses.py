import pefile
import struct
import binascii

def va_to_file_offset(pe, va):
    return pe.get_offset_from_rva(va - pe.OPTIONAL_HEADER.ImageBase)

def main():
    dc4ph_path = r"H:\Games\D.C.4 Da Capo 4 Plus Harmony\DC4PHDL.exe"
    pe = pefile.PE(dc4ph_path)
    with open(dc4ph_path, "rb") as f:
        data = f.read()

    # 1. Get CheckIcon from CallSite
    # CallSite in DC4 was 0x405013, our script found it at 0x405473 in DC4PH.
    # Instruction is E8 XX XX XX XX
    # We found shorter pattern at 0x405473. Wait, our script said:
    # CallSite (DC4 VA: 0x405013) Pattern: 0000e8b6f9ffff80bc24dc0600000074198d8424
    # The actual call is `E8 B6 F9 FF FF`. So `00 00` is before the call.
    # The call instruction itself is at 0x405473 + 2 = 0x405475
    # Let's verify by checking the bytes at 0x405473
    offset = va_to_file_offset(pe, 0x405473)
    call_site_bytes = data[offset:offset+10]
    print(f"Bytes at 0x405473: {binascii.hexlify(call_site_bytes).decode()}")
    
    # Assuming E8 is at offset+2:
    if call_site_bytes[2] == 0xE8:
        rel_offset = struct.unpack('<i', call_site_bytes[3:7])[0]
        call_target = 0x405473 + 2 + 5 + rel_offset
        print(f"CheckIcon = {hex(call_target)}")
        print(f"CallSite = {hex(0x405473 + 2)}")
    
    # 2. Get TABLE_BASE
    # In DC4, table base refs are 0x404f93, 0x4050d5, 0x4051ed, 0x4052b5
    # Let's search for opcode sigs in DC4PH and extract the TABLE_BASE
    # Opcode sig 1: 00005668
    idx = data.find(binascii.unhexlify("00005668"))
    if idx != -1:
        # The next 4 bytes are TABLE_BASE
        table_base = struct.unpack('<I', data[idx+4:idx+8])[0]
        print(f"TABLE_BASE = {hex(table_base)}")
    else:
        print("TABLE_BASE opcodesig 1 not found")

    # 3. Get Real_BacklogFunc
    # In DC4, Real_BacklogFunc is 0x404EE0, call site was 0x405013 (diff +0x133)
    # If call site is 0x405475, then func might be 0x405475 - 0x133 = 0x405342?
    # Actually, functions usually align to 0x10. Let's dump bytes around 0x405320 to 0x4053A0
    offset = va_to_file_offset(pe, 0x405340)
    func_bytes = data[offset-32:offset+32]
    print(f"Bytes around 0x405340: {binascii.hexlify(func_bytes).decode()}")
    
    # Search for Real_BacklogFunc prologue
    # The DC4 prologue: b8 20 27 00 00 e8 56 5c 05 00
    # Let's search for `b8 20 27 00 00` meaning `mov eax, 0x2720` (stack allocation)
    idx_prologue = data.find(binascii.unhexlify("b820270000"))
    if idx_prologue != -1:
        func_va = pe.OPTIONAL_HEADER.ImageBase + pe.get_rva_from_offset(idx_prologue)
        print(f"Possible Real_BacklogFunc = {hex(func_va)}")

if __name__ == '__main__':
    main()
