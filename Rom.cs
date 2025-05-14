using Emulator.Enums.INES;
using Emulator.Mappers;

namespace Emulator;

public class Rom
{
    public const ushort PRG_BANK_SIZE = 0x4000;
    public const ushort CHR_BANK_SIZE = 0x2000;
    
    private bool _useChrRam;

    public MirroringType RomMirroringType;

    public IMapper Mapper { get; }

    public byte ReadPrg(ushort addr)
    {
        return Mapper.ReadPrg(addr);
    }

    public void WriteMemory(ushort addr, byte data)
    {
        switch (addr)
        {
            case >= 0x8000:
            {
                Mapper.SelectedBank = (byte) (data & 0x7);
                break;
            }
        }
    }

    public void WriteChrRam(ushort addr, byte data)
    {
        Mapper.WriteChrRam((ushort) (addr & (CHR_BANK_SIZE - 1)), data);
    }

    public byte ReadChr(ushort addr)
    {
        if (!_useChrRam)
            return Mapper.ReadChrRom(addr);
        
        return Mapper.ReadChrRam(addr);
    }
    
    public Rom(byte[] prg, byte[] chr, byte romFlags6, byte romFlags7, byte prgSize, byte chrSize)
    {
        var prgMask = PRG_BANK_SIZE * prgSize - 1;
        var chrMask = CHR_BANK_SIZE * chrSize - 1;
        _useChrRam = chrSize == 0;
        
        var mapperId = (byte)(((romFlags6 & 0xF0) >> 4) | ((romFlags7 & 0xF) << 4));
        Mapper = MapperFactory.CreateById(mapperId);
        Mapper.Init(prg, chr, prgMask, chrMask);

        RomMirroringType = (byte)(romFlags6 & 0x1) == 0 ? MirroringType.Horizontal : MirroringType.Vertical;
    }
}