namespace Emulator.Mappers;

public class Mapper2 : Mapper
{
    public override byte TotalBanks { get; } = 8;
    public override byte MapperId => 2;
    
    public override byte ReadPrg(ushort addr)
    {
        switch (addr)
        {
            case <= 0x3FFF:
            {
                return PrgData[(addr + Rom.PRG_BANK_SIZE * SelectedBank) & PrgMask];
            }
            case <= 0x7FFF:
            {
                addr -= Rom.PRG_BANK_SIZE;
                return PrgData[(addr + Rom.PRG_BANK_SIZE * (TotalBanks - 1)) & PrgMask];
            }
            default:
                return 1;
        }
    }
}