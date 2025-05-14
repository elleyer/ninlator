namespace Emulator.Mappers;

public interface IMapper
{
    public void Init(byte[] prg, byte[] chr, int prgMask, int chrMask);
    public byte SelectedBank { get; set; }
    public byte MapperId { get; }
    public byte ReadPrg(ushort addr);
    public byte ReadChrRom(ushort addr);
    public byte ReadChrRam(ushort addr);
    public void WriteChrRam(ushort addr, byte data);
}