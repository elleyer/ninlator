namespace Emulator.Mappers;

public class Mapper0 : Mapper
{
    public override byte TotalBanks { get; } = 2;
    public override byte MapperId => 0;
    
    public override byte ReadPrg(ushort addr)
    {
        return PrgData[(ushort) (addr & PrgMask)];
    }
}