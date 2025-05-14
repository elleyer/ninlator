namespace Emulator.Mappers;

public abstract class Mapper : IMapper
{
    protected byte[] PrgData;
    protected byte[] ChrRomData;
    protected byte[] ChrRamData;

    protected int PrgMask;
    protected int ChrMask;

    public void Init(byte[] prg, byte[] chr, int prgMask, int chrMask)
    {
        PrgData = prg;
        ChrRomData = chr;
        PrgMask = prgMask;
        ChrMask = chrMask;
        ChrRamData = new byte[Rom.CHR_BANK_SIZE];
    }

    public byte SelectedBank { get; set; }

    public abstract byte TotalBanks { get; }
    public abstract byte MapperId { get; }

    public byte ReadChrRom(ushort addr)
    {
        return ChrRomData[(ushort) (addr & ChrMask)];
    }
    
    public byte ReadChrRam(ushort addr)
    {
        return ChrRamData[addr];
    }
    
    public void WriteChrRam(ushort addr, byte data)
    {
        ChrRamData[addr] = data;
    }

    public abstract byte ReadPrg(ushort addr);
}