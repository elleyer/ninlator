namespace Emulator;

public class RomLoader
{
    private const int PRG_ROM_SECTOR_SIZE = 16384;
    private const int CHR_ROM_SECTOR_SIZE = 8192;
    private const int TRAINER_SIZE = 512;
    
    public Rom Load(string path)
    {
        var stream = new FileStream(path, FileMode.Open);
        var reader = new BinaryReader(stream);
        var constant = reader.ReadBytes(4);
        var prgRomSize = reader.ReadByte();
        var chrRomSize = reader.ReadByte();
        
        var flags6 = reader.ReadByte();
        var flags7 = reader.ReadByte();
        var flags8 = reader.ReadByte();
        var flags9 = reader.ReadByte();
        var flags10 = reader.ReadByte();
        var padding = reader.ReadBytes(5);

        var trainer = new byte[512];
        var hasTrainer = (flags6 & 0x4) != 0;
        if (hasTrainer)
        {
            trainer = reader.ReadBytes(TRAINER_SIZE);
        }
        
        var prgRom = reader.ReadBytes(prgRomSize * PRG_ROM_SECTOR_SIZE);
        var chrRom = reader.ReadBytes(chrRomSize * CHR_ROM_SECTOR_SIZE);

        return new Rom(prgRom, chrRom, flags6, flags7, prgRomSize, chrRomSize);
    }
}