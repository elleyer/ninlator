namespace Emulator;

public class Ram
{
    private const ushort RamMask = 0x7FF;
    
    //0000 - 07FF
    public byte[] Data = new byte[0x800];
    
    public void Write(ushort addr, byte data) => Data[addr & RamMask] = data;

    public byte Read(ushort addr) =>  Data[addr & RamMask];
}