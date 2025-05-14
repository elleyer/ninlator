using Emulator.Constants;
using Emulator.Controller;
using Emulator.Logs;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Emulator;

public class Emulator
{
    public static bool IsLoggingEnabled;
    public static Logger Logger;
    
    public Ppu Ppu;
    
    private MainBus _bus;
    private Cpu _cpu;
    private Ram _ram;
    private Rom _rom;
    private StandardController _controller;

    //_ppu.NextVBlankCycle + 2 because of the integer rounding.
    long VBlankCycle => (Ppu.NextVBlankCycle + 2) / 3;

    public void Initialize(string romPath, string palettePath)
    {
        var romLoader = new RomLoader();
        _rom = romLoader.Load(romPath);
        _ram = new Ram();

        _controller = new StandardController();

        var palette = SystemPalette.LoadFromFile(palettePath);
        Ppu = new Ppu(_rom, palette);
        
        _cpu = new Cpu();
        _bus = new MainBus();
        _cpu.Initialize(_bus);
        _bus.Initialize(_cpu, Ppu, _ram, _rom, _controller);
        
        ResetCpu();
        
        Ppu.NextVBlankCycle = Ppu.VblankOffset;
    }

    private void ResetCpu()
    {
        var firstByte = _bus.ReadMemoryByAddr(0xFFFC);
        var secondByte = _bus.ReadMemoryByAddr(0xFFFD);
        _cpu.Pc = (ushort)(firstByte | secondByte << 8);
        _cpu.CurrentCycle += 7;
        _cpu.StatusFlags = 0x24;
    }

    public void UpdateInput(KeyboardState keyboardState)
    {
        _controller.UpdateInputData(keyboardState);
    }

    public void Update(double dt)
    {
        var targetCycle = _cpu.CurrentCycle + (int)(Cpu.Frequency * dt);
        while (_cpu.CurrentCycle < targetCycle)
        {
            if (_cpu.CurrentCycle > VBlankCycle)
            {
                Ppu.CatchUpToCpuCycle(_cpu.CurrentCycle);
                if ((Ppu.PpuRegisters[PpuRegisterConstants.PPUCTRL] & 0x80) != 0)
                {
                    _cpu.NonMaskableInterrupt();
                }
            }
            _cpu.ExecuteInstruction();
        }
        Ppu.CatchUpToCpuCycle(_cpu.CurrentCycle);
    }
}