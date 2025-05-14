using Emulator.Constants;
using Emulator.Controller;

namespace Emulator;

public class MainBus
{
    private Cpu _cpu;
    private Ppu _ppu;
    private Ram _ram;
    public Rom _rom;

    private StandardController _controller;
    public void Initialize(Cpu cpu, Ppu ppu, Ram ram, Rom rom, StandardController controller)
    {
        _cpu = cpu;
        _ppu = ppu;
        _ram = ram;
        _rom = rom;
        _controller = controller;
    }
    
    public byte ReadNextByte()
    {
        var data = ReadMemoryByAddr(_cpu.Pc);
        _cpu.Pc += 1;
        return data;
    }
    
    public ushort ReadAddrAbsolute()
    {
        return (ushort)(ReadNextByte() | ReadNextByte() << 8);
    }

    //16-bit addressing mode.
    public byte ReadMemoryByAddr(ushort address)
    {
        switch (address)
        {
            case <= 0x1FFF:
                return _ram.Read(address);
            case <= 0x2007:
            {
                _ppu.CatchUpToCpuCycle(_cpu.CurrentCycle);
                return _ppu.ReadRegister((byte)(address - 0x2000));
            }
            case 0x4016:
                return (byte) (_controller.ReadNextButton() + 0x40);
            case >= 0x8000:
                return _rom.ReadPrg((ushort) (address - 0x8000));
        }
        return 0;
    }

    public void WriteMemory(ushort address, byte data)
    {
        switch (address)
        {
            //Stack
            case >= 0x0100 and <= 0x01FF:
                _ram.Write(address, data);
                break;
            //Ram
            case <= 0x07FF:
                _ram.Write(address, data);
                break;
            //PPU
            case >= 0x2000 and <= 0x2007:
            {
                _ppu.CatchUpToCpuCycle(_cpu.CurrentCycle);
                _ppu.WriteRegister((byte)(address - 0x2000), data);
                break;
            }
            //OAM DMA
            case 0x4014:
            {
                for (var i = 0; i < 256; i++)
                {
                    var addr = (ushort)((data << 8) | i);
                    var spriteData = ReadMemoryByAddr(addr);
                    _ppu.WriteRegister(PpuRegisterConstants.OAMDATA, spriteData);
                }
                _cpu.CurrentCycle += 513;
                break;
            }
            //Controller
            case 0x4016:
            {
                _controller.CheckButtons(data == 0);
                break;
            }
            case >= 0x8000:
            {
                _rom.WriteMemory(address, data);
                break;
            }
        }
    }
}