using Emulator.Constants;

namespace Emulator;

public class Cpu
{
    public const long Frequency = 1790000;
    private const ushort StackAddr = 0x100;

    public long CurrentCycle;
    
    public byte Accumulator;
    public byte X;
    public byte Y;
    public byte StackPointer;
    public byte StatusFlags;
    
    public ushort Pc { get; set; }

    private MainBus _bus;
    public void Initialize(MainBus bus)
    {
        _bus = bus;
    }
    
    private void StackPush(byte value)
    {
        _bus.WriteMemory((ushort)(StackAddr | StackPointer), value);
        StackPointer--;
    }

    private byte StackPull()
    {
        StackPointer++;
        var data = _bus.ReadMemoryByAddr((ushort)(StackAddr | StackPointer));
        return data;
    }

    public void NonMaskableInterrupt()
    {
        //read the NMI handler's address from $FFFA-$FFFB
        var addr = Pc;
        StackPush((byte)(addr >> 8));
        StackPush((byte)addr);
        StackPush(StatusFlags);
        var firstByte = _bus.ReadMemoryByAddr(0xFFFA);
        var secondByte = _bus.ReadMemoryByAddr(0xFFFB);
        Pc = (ushort)(firstByte | secondByte << 8);
        CurrentCycle += 7;
    }
    
    public void UpdateStatusFlags(byte register)
    {
        AssignFlag(StatusFlagsConstants.ZERO,
            register == 0);
        AssignFlag(StatusFlagsConstants.NEGATIVE,
            (register & StatusFlagsConstants.NEGATIVE) != 0);
    }

    private void AssignFlag(byte mask, bool condition)
    {
        if (condition)
            StatusFlags |= mask;
        else
            StatusFlags &= (byte)~mask;
    }
    
    public void ExecuteInstruction()
    {
        var opcode = _bus.ReadNextByte();
        if (Emulator.IsLoggingEnabled)
        {
            Emulator.Logger.Log($"{(Pc+1):X4}, {opcode:X2}, " +
                                $"A:{Accumulator:X2} X:{X:X2}, Y:{Y:X2}, P:{StatusFlags:X2}, " +
                                $"SP:{StackPointer:X2} CYC:{CurrentCycle}");
        }
        
        switch (opcode)
        {
            //SEI (Set Interrupt Disable)
            case 0x78:
                AssignFlag(StatusFlagsConstants.INTERRUPT_DISABLE, true);
                CurrentCycle += 2;
                break;
            //LDA (Load Accumulator)
            case 0xA9:
                Accumulator = _bus.ReadNextByte();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            //STA: Store Accumulator
            case 0x8D:
            {
                //Read 2 bytes and merge them to get the address.
                var addr = _bus.ReadAddrAbsolute();
                _bus.WriteMemory(addr, Accumulator);
                CurrentCycle += 4;
                break;
            }
            //CLD - Clear Decimal Mode
            case 0xD8:
                AssignFlag(StatusFlagsConstants.DECIMAL, false);
                CurrentCycle += 2;
                break;
            //LDX - Load X Register (Immediate)
            case 0xA2:
                X = _bus.ReadNextByte();
                UpdateStatusFlags(X);
                CurrentCycle += 2;
                break;
            //LDA - Load Accumulator (Absolute)
            case 0xAD:
            {
                var addr = _bus.ReadAddrAbsolute();
                Accumulator = _bus.ReadMemoryByAddr(addr);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //LDA - Load Accumulator (Zero Page)
            case 0xA5:
            {
                var addrByte = _bus.ReadNextByte();
                Accumulator = _bus.ReadMemoryByAddr(addrByte);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 3;
                break;
            }
            //BPL - Branch if positive
            case 0x10:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.NEGATIVE) == 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //BNE - Branch if Not Equal
            case 0xD0:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.ZERO) == 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //DEX - Decrement X Register
            case 0xCA:
            {
                X -= 1;
                UpdateStatusFlags(X);
                CurrentCycle += 2;
                break;
            }
            //TXS - Transfer X to Stack Pointer
            case 0x9A:
            {
                StackPointer = X;
                CurrentCycle += 2;
                break;
            }
            //JSR - Jump to Subroutine
            case 0x20:
            {
                var jumpAddress = _bus.ReadAddrAbsolute();
                var returnAddress = (ushort)(Pc - 1);
                StackPush((byte)(returnAddress >> 8));
                StackPush((byte)returnAddress);
                Pc = jumpAddress;
                CurrentCycle += 6;
                break;
            }
            //STA - Store Accumulator (Zero page)
            case 0x85:
            {
                var address = _bus.ReadNextByte();
                _bus.WriteMemory(address, Accumulator);
                CurrentCycle += 3;
                break;
            }
            //RTS - Return from Subroutine
            case 0x60:
            {
                var addr = (ushort)(StackPull() | StackPull() << 8);
                Pc = (ushort)(addr + 1);
                CurrentCycle += 6;
                break;
            }
            //LDY - Load Y Register (Immediate)
            case 0xA0:
            {
                Y = _bus.ReadNextByte();
                UpdateStatusFlags(Y);
                CurrentCycle += 2;
                break;
            }
            //STY - Store Y Register
            case 0x8C:
            {
                //Read 2 bytes and merge them to get the address.
                var addr = _bus.ReadAddrAbsolute();
                _bus.WriteMemory(addr, Y);
                CurrentCycle += 4;
                break;
            }
            //LDA - Load Accumulator (absolute,X)
            case 0xBD:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + X);
                
                Accumulator = _bus.ReadMemoryByAddr(addrX);
                CheckIfPageCrossed(addr, addrX);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //INX - Increment X Register
            case 0xE8:
            {
                X += 1;
                UpdateStatusFlags(X);
                CurrentCycle += 2;
                break;
            }
            //DEY - Decrement Y Register
            case 0x88:
            {
                Y -= 1;
                UpdateStatusFlags(Y);
                CurrentCycle += 2;
                break;
            }
            //TXA - Transfer X to Accumulator
            case 0x8A:
            {
                Accumulator = X;
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //CLC - Clear Carry Flag
            case 0x18:
            {
                AssignFlag(StatusFlagsConstants.CARRY, false);
                CurrentCycle += 2;
                break;
            }
            //ADC - Add with Carry (Zero page)
            case 0x65:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                AddWithCarry(b);
                CurrentCycle += 3;
                break;
            }
            //ASL - Arithmetic Shift Left (Accumulator)
            case 0x0A:
            {
                var oldByte = Accumulator;
                var newByte = (byte) (oldByte << 1);
                Accumulator = newByte;
                AssignFlag(StatusFlagsConstants.CARRY, (oldByte & 0b_1000_0000) != 0);
                CurrentCycle += 2;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //ASL - Arithmetic Shift Left (Absolute)
            case 0x0E:
            {
                var addr = _bus.ReadAddrAbsolute();
                var oldByte = _bus.ReadMemoryByAddr(addr);
                var newByte = (byte) (oldByte << 1);
                _bus.WriteMemory(addr, newByte);
                AssignFlag(StatusFlagsConstants.CARRY, (oldByte & 0x80) != 0);
                UpdateStatusFlags(newByte);
                CurrentCycle += 6;
                break;
            }
            //STA - Store Accumulator (Absolute,X)
            case 0x9D:
            {
                var addr = (ushort)(_bus.ReadAddrAbsolute() + X);
                _bus.WriteMemory(addr, Accumulator);
                CurrentCycle += 5;
                break;
            }
            //CPX - Compare X Register (Immediate)
            case 0xE0:
            {
                var memValue = _bus.ReadNextByte();
                var result = (byte) (X - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, X >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 2;
                break;
            }
            //TAX - Transfer Accumulator to X
            case 0xAA:
            {
                X = Accumulator;
                UpdateStatusFlags(X);
                CurrentCycle += 2;
                break;
            }
            //LDX - Load X Register (Zero Page)
            case 0xA6:
            {
                var addrByte = _bus.ReadNextByte();
                X = _bus.ReadMemoryByAddr(addrByte);
                UpdateStatusFlags(X);
                CurrentCycle += 3;
                break;
            }
            //EOR - Exclusive OR (Immediate)
            case 0x49:
            {
                Accumulator ^= _bus.ReadNextByte();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //ADC - Add with Carry (Immediate)
            case 0x69:
            {
                var b = _bus.ReadNextByte();
                AddWithCarry(b);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //STX - Store X Register (Zero Page)
            case 0x86:
            {
                _bus.WriteMemory(_bus.ReadNextByte(), X);
                CurrentCycle += 3;
                break;
            }
            //STA - Store Accumulator (Zero Page, X)
            case 0x95:
            {
                var addr = (byte)(_bus.ReadNextByte() + X);
                _bus.WriteMemory(addr, Accumulator);
                CurrentCycle += 4;
                break;
            }
            //CMP - Compare (Absolute, X)
            case 0xDD:
            {
                var originalAddr = _bus.ReadAddrAbsolute();
                var indexedAddr = (ushort) (originalAddr + X);
                
                var memValue = _bus.ReadMemoryByAddr(indexedAddr);
                var result = (byte) (Accumulator - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= memValue);
                UpdateStatusFlags(result);
                CheckIfPageCrossed(originalAddr, indexedAddr);
                CurrentCycle += 4;
                break;
            }
            //TAY - Transfer Accumulator to Y
            case 0xA8:
            {
                Y = Accumulator;
                UpdateStatusFlags(Y);
                CurrentCycle += 2;
                break;
            }
            //LDA - Load Accumulator (Indirect), Y
            case 0xB1:
            {
                Accumulator = ReadByteIndirectY();
                CurrentCycle += 5;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //BCC - Branch if Carry Clear
            case 0x90:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.CARRY) == 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //CMP - Compare (Immediate)
            case 0xC9:
            {
                var b = _bus.ReadNextByte();
                var result = (byte) (Accumulator - b);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 2;
                break;
            }
            //INC - Increment Memory (Zero Page)
            case 0xE6:
            {
                var addr = _bus.ReadNextByte();
                var result = (byte) (_bus.ReadMemoryByAddr(addr) + 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 5;
                break;
            }
            //PHA - Push Accumulator
            case 0x48:
            {
                StackPush(Accumulator);
                CurrentCycle += 3;
                break;
            }
            //TYA - Transfer Y to Accumulator
            case 0x98:
            {
                Accumulator = Y;
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //PHP - Push Processor Status
            case 0x08:
            {
                StackPush(StatusFlags);
                CurrentCycle += 3;
                break;
            }
            //CPX - Compare X Register (Zero page)
            case 0xE4:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                var result = (byte) (X - b);
                AssignFlag(StatusFlagsConstants.CARRY, X >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 3;
                break;
            }
            //BEQ - Branch if Equal
            case 0xF0:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.ZERO) != 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //BRK - Force Interrupt
            case 0x00:
            {
                //read the IRQ interrupt vector from $FFFE-$FFFF
                StackPush((byte)(Pc >> 8));
                StackPush((byte)Pc);
                StackPush(StatusFlags);
                var firstByte = _bus.ReadMemoryByAddr(0xFFFE);
                var secondByte = _bus.ReadMemoryByAddr(0xFFFF);
                Pc = (ushort)(firstByte | secondByte << 8);
                CurrentCycle += 7;
                break;
            }
            //BMI - Branch if Minus
            case 0x30:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.NEGATIVE) != 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //ORA - Logical Inclusive OR (Immediate)
            case 0x09:
            {
                Accumulator |= _bus.ReadNextByte();
                CurrentCycle += 2;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //STX - Store X Register (Absolute)
            case 0x8E:
            {
                var addr = _bus.ReadAddrAbsolute();
                _bus.WriteMemory(addr, X);
                CurrentCycle += 4;
                break;
            }
            //STY - Store Y Register (Zero Page)
            case 0x84:
            {
                var addr = _bus.ReadNextByte();
                _bus.WriteMemory(addr, Y);
                CurrentCycle += 3;
                break;
            }
            //AND - Logical AND (Immediate)
            case 0x29:
            {
                var b = _bus.ReadNextByte();
                Accumulator &= b;
                CurrentCycle += 2;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //ROR - Rotate Right (Zero Page)
            case 0x66:
            {
                var addr = _bus.ReadNextByte();
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue >> 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0b_1000_0000;
                
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & StatusFlagsConstants.CARRY) != 0);
                UpdateStatusFlags(newValue);
                _bus.WriteMemory(addr, newValue);
                CurrentCycle += 5;
                break;
            }
            //LDA - Load Accumulator (Zero Page, X)
            case 0xB5:
            {
                Accumulator = _bus.ReadMemoryByAddr((byte)(_bus.ReadNextByte() + X));
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //AND - Logical AND (Zero Page)
            case 0x25:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                Accumulator &= b;
                CurrentCycle += 3;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //CMP - Compare (Zero Page)
            case 0xC5:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                var result = (byte) (Accumulator - b);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 3;
                break;
            }
            //PLP - Pull Processor Status
            case 0x28:
            {
                StatusFlags = StackPull();
                CurrentCycle += 4;
                break;
            }
            //PLA - Pull Accumulator
            case 0x68:
            {
                if (CurrentCycle == 248)
                {
                    Console.WriteLine();
                }
                Accumulator = StackPull();
                CurrentCycle += 4;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //RTI - Return from Interrupt
            case 0x40:
            {
                StatusFlags = StackPull();
                var addr = (ushort)(StackPull() | (StackPull() << 8));
                Pc = addr;
                CurrentCycle += 6;
                break;
            }
            //SEC - Set Carry Flag
            case 0x38:
            {
                AssignFlag(StatusFlagsConstants.CARRY, true);
                CurrentCycle += 2;
                break;
            }
            //SBC - Subtract with Carry (Zero Page)
            case 0xE5:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                AddWithCarry((byte)~b);
                CurrentCycle += 3;
                break;
            }
            //ADC - Add with Carry (Zero page, X)
            case 0x75:
            {
                var b = _bus.ReadMemoryByAddr((byte)(_bus.ReadNextByte() + X));
                AddWithCarry(b);
                CurrentCycle += 4;
                break;
            }
            //STA - Store Accumulator (Indirect),Y
            case 0x91:
            {
                var addr = ReadAddrIndirectY();
                _bus.WriteMemory(addr, Accumulator);
                CurrentCycle += 6;
                break;
            }
            //INY - Increment Y Register
            case 0xC8:
            {
                Y++;
                UpdateStatusFlags(Y);
                CurrentCycle += 2;
                break;
            }
            //LDY - Load Y Register (Zero Page)
            case 0xA4:
            {
                Y = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                UpdateStatusFlags(Y);
                CurrentCycle += 3;
                break;
            }
            //JMP - Jump (Absolute)
            case 0x4C:
            {
                Pc = _bus.ReadAddrAbsolute();
                CurrentCycle += 3;
                break;
            }
            //DEC - Decrement Memory (Zero Page)
            case 0xC6:
            {
                var addr = _bus.ReadNextByte();
                if (addr == 0x20)
                {
                    Console.WriteLine("");
                }
                var value = _bus.ReadMemoryByAddr(addr);
                value -= 1;
                _bus.WriteMemory(addr, value);
                UpdateStatusFlags(value);
                CurrentCycle += 5;
                break;
            }
            //LSR - Logical Shift Right (Accumulator)
            case 0x4A:
            {
                var acc = Accumulator;
                Accumulator >>= 1;
                AssignFlag(StatusFlagsConstants.CARRY, (acc & 0x1) != 0);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //ORA - Logical Inclusive OR (Zero Page)
            case 0x05:
            {
                Accumulator |= _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 3;
                break;
            }
            //AND - Logical AND (Indirect),Y
            case 0x31:
            {
                var b = ReadByteIndirectY();
                Accumulator &= b;
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 5;
                break;
            }
            //LSR - Logical Shift Right (Zero Page)
            case 0x46:
            {
                var addr = _bus.ReadNextByte();
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old >> 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x1) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 5;
                break;
            }
            //ASL - Arithmetic Shift Left (Zero Page)
            case 0x06:
            {
                var addr = _bus.ReadNextByte();
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old << 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x80) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 5;
                break;
            }
            //SBC - Subtract with Carry (Immediate)
            case 0xE9:
            {
                var b = _bus.ReadNextByte();
                AddWithCarry((byte)~b);
                CurrentCycle += 2;
                break;
            }
            //ORA - Logical Inclusive OR (Indirect), Y
            case 0x11:
            {
                Accumulator |= ReadByteIndirectY();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 5;
                break;
            }
            //LDA - Load Accumulator (Absolute, Y)
            case 0xB9:
            {
                Accumulator = ReadByteAbsoluteY();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //Jump (Indirect)
            case 0x6C:
            {
                var addr = _bus.ReadAddrAbsolute();
                var lsb = _bus.ReadMemoryByAddr(addr);
                var msbAddr = (ushort)(addr + 1);
                if ((addr & 0xFF) == 0xFF)
                {
                    msbAddr -= 0x100;
                }
                var msb = _bus.ReadMemoryByAddr(msbAddr);
                Pc = (ushort) ((msb << 8) | lsb);
                CurrentCycle += 5;
                break;
            }
            //LDY - Load Y Register (Zero Page, X)
            case 0xB4:
            {
                var addr = _bus.ReadNextByte();
                Y = _bus.ReadMemoryByAddr((byte) (addr + X));
                UpdateStatusFlags(Y);   
                CurrentCycle += 4;
                break;
            }
            //STA - Store Accumulator (Absolute, Y)
            case 0x99:
            {
                var addr = _bus.ReadAddrAbsolute() + Y;
                _bus.WriteMemory((ushort) addr, Accumulator);
                CurrentCycle += 5;
                break;
            }
            //BCS - Branch if Carry Set
            case 0xB0:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //EOR - Exclusive OR (Zero Page)
            case 0x45:
            {
                var addr = _bus.ReadNextByte();
                Accumulator ^= _bus.ReadMemoryByAddr(addr);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 3;
                break;
            }
            //Increment memory (Zero Page,X)
            case 0xF6:
            {
                var addr = (byte)(_bus.ReadNextByte() + X);
                var result = (byte) (_bus.ReadMemoryByAddr(addr) + 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //CPY - Compare Y Register (Immediate)
            case 0xC0:
            {
                var memValue = _bus.ReadNextByte();
                var result = (byte) (Y - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Y >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 2;
                break;
            }
            //ORA - Logical Inclusive OR (Zero page, X)
            case 0x15:
            {
                Accumulator |= _bus.ReadMemoryByAddr((byte) (_bus.ReadNextByte() + X));
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //SBC - Subtract with Carry (Zero Page, X)
            case 0xF5:
            {
                var b = _bus.ReadMemoryByAddr((byte) (_bus.ReadNextByte() + X));
                AddWithCarry((byte)~b);
                CurrentCycle += 4;
                break;
            }
            //DEC - Decrement Memory (Zero Page, X)
            case 0xD6:
            {
                var addr = (byte)(_bus.ReadNextByte() + X);
                var result = (byte) (_bus.ReadMemoryByAddr(addr) - 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //NOP
            case 0xEA:
            {
                CurrentCycle += 2;
                break;
            }
            //LDX - Load X Register (Absolute)
            case 0xAE:
            {
                var addr = _bus.ReadAddrAbsolute();
                X = _bus.ReadMemoryByAddr(addr);
                UpdateStatusFlags(X);
                CurrentCycle += 4;
                break;
            }
            //ROR - Rotate Right (Absolute)
            case 0x6E:
            {
                var addr = _bus.ReadAddrAbsolute();
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue >> 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0b_1000_0000;
                
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & StatusFlagsConstants.CARRY) != 0);
                UpdateStatusFlags(newValue);
                _bus.WriteMemory(addr, newValue);
                CurrentCycle += 6;
                break;
            }
            //ROL - Rotate Left (Accumulator)
            case 0x2A:
            {
                var oldValue = Accumulator;
                var newValue = (byte) ((oldValue << 1) & 0xFF);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0x1;

                Accumulator = newValue;
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & 0x80) != 0);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //INC - Increment Memory (Absolute)
            case 0xEE:
            {
                var addr = _bus.ReadAddrAbsolute();
                var result = (byte) (_bus.ReadMemoryByAddr(addr) + 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //DEC - Decrement Memory (Absolute)
            case 0xCE:
            {
                var addr = _bus.ReadAddrAbsolute();
                var result = (byte) (_bus.ReadMemoryByAddr(addr) - 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //ADC - Add with Carry (Absolute)
            case 0x6D:
            {
                var b = _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                AddWithCarry(b);
                CurrentCycle += 4;
                break;
            }
            //LDY - Load Y Register (Absolute)
            case 0xAC:
            {
                Y = _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                UpdateStatusFlags(Y);
                CurrentCycle += 4;
                break;
            }
            //ROR - Rotate Right (Accumulator)
            case 0x6A:
            {
                var oldValue = Accumulator;
                var newValue = Accumulator >> 1;
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0b_1000_0000;
                
                Accumulator = (byte) newValue;
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & StatusFlagsConstants.CARRY) != 0);
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 2;
                break;
            }
            //INC - Increment Memory (Absolute, X)
            case 0xFE:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var result = (byte) (_bus.ReadMemoryByAddr(addr) + 1);
                _bus.WriteMemory(addr, result);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //BIT - Bit Test (Zero Page)
            case 0x24:
            {
                var memValue = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                var result = Accumulator & memValue;
                AssignFlag(StatusFlagsConstants.ZERO, result == 0);
                AssignFlag(StatusFlagsConstants.NEGATIVE, (memValue & 0x80) != 0);
                AssignFlag(StatusFlagsConstants.OVERFLOW, (memValue & 0x40) != 0);
                CurrentCycle += 3;
                break;
            }
            //BIT - Bit Test (Absolute)
            case 0x2C:
            {
                var memValue = _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                var result = Accumulator & memValue;
                AssignFlag(StatusFlagsConstants.ZERO, result == 0);
                AssignFlag(StatusFlagsConstants.NEGATIVE, (memValue & 0x80) != 0);
                AssignFlag(StatusFlagsConstants.OVERFLOW, (memValue & 0x40) != 0);
                CurrentCycle += 4;
                break;
            }
            //BVS - Branch if Overflow Set
            case 0x70:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.OVERFLOW) != 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //ROL - Rotate Left (Zero Page)
            case 0x26:
            {
                var addr = _bus.ReadNextByte();
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue << 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0x1;

                _bus.WriteMemory(addr, newValue);
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & 0x80) != 0);
                UpdateStatusFlags(newValue);
                CurrentCycle += 5;
                break;
            }
            //ORA - Logical Inclusive OR (Absolute)
            case 0x0D:
            {
                Accumulator |= _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //LDY - Load Y Register (Absolute, X)
            case 0xBC:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + X);
                Y = _bus.ReadMemoryByAddr(addrX);
                UpdateStatusFlags(Y);
                CheckIfPageCrossed(addr, addrX);
                CurrentCycle += 3;
                break;
            }
            //LDX - Load X Register (Absolute, Y)
            case 0xBE:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + Y);
                X = _bus.ReadMemoryByAddr(addrX);
                UpdateStatusFlags(X);
                CheckIfPageCrossed(addr, addrX);
                CurrentCycle += 3;
                break;
            }
            //CMP - Compare (Absolute)
            case 0xCD:
            {
                var addr = _bus.ReadAddrAbsolute();
                var memValue = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (Accumulator - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 4;
                break;
            }
            //CMP - Compare (Absolute, Y)
            case 0xD9:
            {
                var originalAddr = _bus.ReadAddrAbsolute();
                var indexedAddr = (ushort) (originalAddr + Y);
                
                var memValue = _bus.ReadMemoryByAddr(indexedAddr);
                var result = (byte) (Accumulator - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= memValue);
                UpdateStatusFlags(result);
                CheckIfPageCrossed(originalAddr, indexedAddr);
                CurrentCycle += 4;
                break;
            }
            //DEC - Decrement Memory (Absolute, X)
            case 0xDE:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var value = _bus.ReadMemoryByAddr(addr);
                value -= 1;
                _bus.WriteMemory(addr, value);
                UpdateStatusFlags(value);
                CurrentCycle += 7;
                break;
            }
            //LSR - Logical Shift Right (Zero Page, X)
            case 0x56:
            {
                var addr = (byte) (_bus.ReadNextByte() + X);
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old >> 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x1) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //CMP - Compare (Zero Page)
            case 0xD5:
            {
                var b = _bus.ReadMemoryByAddr((byte) (_bus.ReadNextByte() + X));
                var result = (byte) (Accumulator - b);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 3;
                break;
            }
            //ORA - Logical Inclusive OR (Zero page, X)
            case 0x1D:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + X);
                Accumulator |= _bus.ReadMemoryByAddr(addrX);
                UpdateStatusFlags(Accumulator);
                CheckIfPageCrossed(addr, addrX);
                CurrentCycle += 4;
                break;
            }
            //ADC - Add with Carry (Absolute, Y)
            case 0x79:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrY = (ushort) (addr + Y);
                var b = _bus.ReadMemoryByAddr(addrY);
                AddWithCarry(b);
                CurrentCycle += 4;
                CheckIfPageCrossed(addr, addrY);
                break;
            }
            //LDX - Load X Register (Zero Page, Y)
            case 0xB6:
            {
                var addrByte = _bus.ReadNextByte();
                X = _bus.ReadMemoryByAddr((byte) (addrByte + Y));
                UpdateStatusFlags(X);
                CurrentCycle += 4;
                break;
            }
            //STX - Store X Register (Zero Page, Y)
            case 0x96:
            {
                _bus.WriteMemory((byte) (_bus.ReadNextByte() + Y), X);
                CurrentCycle += 4;
                break;
            }
            //CPY - Compare Y Register (Zero Page)
            case 0xC4:
            {
                var memValue = _bus.ReadMemoryByAddr(_bus.ReadNextByte());
                var result = (byte) (Y - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Y >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 3;
                break;
            }
            //CPX - Compare X Register (Absolute)
            case 0xEC:
            {
                var memValue = _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                var result = (byte) (X - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, X >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 4;
                break;
            }
            //ROR - Rotate Right (Zero Page, X)
            case 0x76:
            {
                var addr = (byte) (_bus.ReadNextByte() + X);
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = oldValue >> 1;
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0b_1000_0000;
                
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & StatusFlagsConstants.CARRY) != 0);
                UpdateStatusFlags((byte)newValue);
                _bus.WriteMemory(addr, (byte) newValue);
                CurrentCycle += 6;
                break;
            }
            //ROR - Rotate Right (Absolute, X)
            case 0x7E:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = oldValue >> 1;
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0b_1000_0000;
                
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & StatusFlagsConstants.CARRY) != 0);
                UpdateStatusFlags((byte)newValue);
                _bus.WriteMemory(addr, (byte) newValue);
                CurrentCycle += 7;
                break;
            }
            
            //ROL - Rotate Left (Absolute, X)
            case 0x3E:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue << 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0x1;

                _bus.WriteMemory(addr, newValue);
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & 0x80) != 0);
                UpdateStatusFlags(newValue);
                CurrentCycle += 7;
                break;
            }
            
            //SBC - Subtract with Carry (Absolute, X)
            case 0xFD:
            {
                var b = _bus.ReadMemoryByAddr((ushort) (_bus.ReadAddrAbsolute() + X));
                AddWithCarry((byte)~b);
                CurrentCycle += 4;
                break;
            }
            //ADC - Add with Carry (Absolute, X)
            case 0x7D:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + X);
                var b = _bus.ReadMemoryByAddr(addrX);
                AddWithCarry(b);
                CurrentCycle += 4;
                CheckIfPageCrossed(addr, addrX);
                break;
            }
            //ADC - Add with Carry (Indirect),Y)
            case 0x71:
            {
                var b = _bus.ReadMemoryByAddr(ReadAddrIndirectY());
                AddWithCarry(b);
                CurrentCycle += 5;
                break;
            }
            //AND - Logical AND (Zero Page, X)
            case 0x35:
            {
                var b = _bus.ReadMemoryByAddr((byte) (_bus.ReadNextByte() + X));
                Accumulator &= b;
                CurrentCycle += 4;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //STY - Store Y Register (Zero Page, X)
            case 0x94:
            {
                var addr = (byte) (_bus.ReadNextByte() + X);
                _bus.WriteMemory(addr, Y);
                CurrentCycle += 4;
                break;
            }
            //CPY - Compare Y Register (Absolute)
            case 0xCC:
            {
                var memValue = _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
                var result = (byte) (Y - memValue);
                AssignFlag(StatusFlagsConstants.CARRY, Y >= memValue);
                UpdateStatusFlags(result);
                CurrentCycle += 4;
                break;
            }
            //TSX - Transfer Stack Pointer to X
            case 0xBA:
            {
                X = StackPointer;
                UpdateStatusFlags(X);
                CurrentCycle += 2;
                break;
            }
            //EOR - Exclusive OR (Absolute, X)
            case 0x5D:
            {
                var addr = _bus.ReadAddrAbsolute();
                var addrX = (ushort) (addr + X);
                Accumulator ^= _bus.ReadMemoryByAddr(addrX);
                UpdateStatusFlags(Accumulator);
                CheckIfPageCrossed(addr, addrX);
                CurrentCycle += 4;
                break;
            }
            //SED - Set Decimal Flag
            case 0xF8:
            {
                AssignFlag(StatusFlagsConstants.DECIMAL, true);
                CurrentCycle += 2;
                break;
            }
            //CLI - Clear Interrupt Disable
            case 0x58:
            {
                AssignFlag(StatusFlagsConstants.INTERRUPT_DISABLE, false);
                CurrentCycle += 2;
                break;
            }
            //CLV - Clear Overflow Disable
            case 0xB8:
            {
                AssignFlag(StatusFlagsConstants.OVERFLOW, false);
                CurrentCycle += 2;
                break;
            }
            //BVC - Branch if Overflow Clear
            case 0x50:
            {
                var b = _bus.ReadNextByte();
                CurrentCycle += 2;
                if ((StatusFlags & StatusFlagsConstants.OVERFLOW) == 0)
                {
                    CurrentCycle += 1;
                    var prevPc = Pc;
                    Pc = (ushort)(Pc + (sbyte)b);
                    CheckIfPageCrossed(prevPc, Pc);
                }
                break;
            }
            //LDA - Load Accumulator (Indirect, X)
            case 0xA1:
            {
                Accumulator = ReadByteIndirectX();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 6;
                break;
            }
            //STA: Store Accumulator (Indirect,X))
            case 0x81:
            {
                //Read 2 bytes and merge them to get the address.
                var addr = ReadAddrIndirectX();
                _bus.WriteMemory(addr, Accumulator);
                CurrentCycle += 6;
                break;
            }
            //ORA - Logical Inclusive OR (Indirect, X)
            case 0x01:
            {
                Accumulator |= ReadByteIndirectX();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 6;
                break;
            }
            //AND - Logical AND (Indirect, X)
            case 0x21:
            {
                var b = ReadByteIndirectX();
                Accumulator &= b;
                CurrentCycle += 6;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //EOR - Exclusive OR (Indirect, X)
            case 0x41:
            {
                Accumulator ^= ReadByteIndirectX();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 6;
                break;
            }
            //ADC - Add with Carry (Indirect, X)
            case 0x61:
            {
                var b = ReadByteIndirectX();
                AddWithCarry(b);
                CurrentCycle += 6;
                break;
            }
            //CMP - Compare (Indirect, X)
            case 0xC1:
            {
                var b = ReadByteIndirectX();
                var result = (byte) (Accumulator - b);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //SBC - Subtract with Carry (Indirect, X)
            case 0xE1:
            {
                var b = ReadByteIndirectX();
                AddWithCarry((byte)~b);
                CurrentCycle += 6;
                break;
            }
            //AND - Logical AND (Absolute)
            case 0x2D:
            {
                var b = ReadByteAbsolute();
                Accumulator &= b;
                CurrentCycle += 4;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //EOR - Exclusive OR (Absolute)
            case 0x4D:
            {
                Accumulator ^= ReadByteAbsolute();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //SBC - Subtract with Carry (Absolute)
            case 0xED:
            {
                var b = ReadByteAbsolute();
                AddWithCarry((byte)~b);
                CurrentCycle += 4;
                break;
            }
            //LSR - Logical Shift Right (Absolute)
            case 0x4E:
            {
                var addr = _bus.ReadAddrAbsolute();
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old >> 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x1) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 5;
                break;
            }
            //ROL - Rotate Left (Absolute)
            case 0x2E:
            {
                var addr = _bus.ReadAddrAbsolute();
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue << 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0x1;

                _bus.WriteMemory(addr, newValue);
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & 0x80) != 0);
                UpdateStatusFlags(newValue);
                CurrentCycle += 6;
                break;
            }
            //EOR - Exclusive OR (Indirect, Y)
            case 0x51:
            {
                Accumulator ^= ReadByteIndirectY();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 5;
                break;
            }
            //CMP - Compare (Indirect, Y)
            case 0xD1:
            {
                var b = ReadByteIndirectY();
                var result = (byte) (Accumulator - b);
                AssignFlag(StatusFlagsConstants.CARRY, Accumulator >= b);
                UpdateStatusFlags(result);
                CurrentCycle += 5;
                break;
            }
            //SBC - Subtract with Carry (Indirect, Y)
            case 0xF1:
            {
                var b = ReadByteIndirectY();
                AddWithCarry((byte)~b);
                CurrentCycle += 5;
                break;
            }
            //ORA - Logical Inclusive OR (Absolute, Y)
            case 0x19:
            {
                Accumulator |= ReadByteAbsoluteY();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //AND - Logical AND (Absolute, Y)
            case 0x39:
            {
                var b = ReadByteAbsoluteY();
                Accumulator &= b;
                CurrentCycle += 4;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //EOR - Exclusive OR (Absolute, Y)
            case 0x59:
            {
                Accumulator ^= ReadByteAbsoluteY();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //SBC - Subtract with Carry (Absolute, Y)
            case 0xF9:
            {
                var b = ReadByteAbsoluteY();
                AddWithCarry((byte)~b);
                CurrentCycle += 4;
                break;
            }
            //EOR - Exclusive OR (Zero Page, X)
            case 0x55:
            {
                Accumulator ^= ReadByteZeroPageX();
                UpdateStatusFlags(Accumulator);
                CurrentCycle += 4;
                break;
            }
            //ASL - Arithmetic Shift Left (Zero Page, X)
            case 0x16:
            {
                var addr = (byte) (_bus.ReadNextByte() + X);
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old << 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x80) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 6;
                break;
            }
            //ROL - Rotate Left (Zero Page, X)
            case 0x36:
            {
                var addr = (byte) (_bus.ReadNextByte() + X);
                var oldValue = _bus.ReadMemoryByAddr(addr);
                var newValue = (byte) (oldValue << 1);
                
                if ((StatusFlags & StatusFlagsConstants.CARRY) != 0)
                    newValue |= 0x1;

                _bus.WriteMemory(addr, newValue);
                AssignFlag(StatusFlagsConstants.CARRY, (oldValue & 0x80) != 0);
                UpdateStatusFlags(newValue);
                CurrentCycle += 6;
                break;
            }
            case 0x3D:
            {
                var b = ReadByteAbsoluteX();
                Accumulator &= b;
                CurrentCycle += 4;
                UpdateStatusFlags(Accumulator);
                break;
            }
            //LSR - Logical Shift Right (Abs, X)
            case 0x5E:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var old = _bus.ReadMemoryByAddr(addr);
                var result = (byte) (old >> 1);
                _bus.WriteMemory(addr, result);
                AssignFlag(StatusFlagsConstants.CARRY, (old & 0x1) != 0);
                UpdateStatusFlags(result);
                CurrentCycle += 7;
                break;
            }
            //ASL - Arithmetic Shift Left (Abs, X)
            case 0x1E:
            {
                var addr = (ushort) (_bus.ReadAddrAbsolute() + X);
                var oldByte = _bus.ReadMemoryByAddr(addr);
                var newByte = (byte) (oldByte << 1);
                _bus.WriteMemory(addr, newByte);
                AssignFlag(StatusFlagsConstants.CARRY, (oldByte & 0x80) != 0);
                UpdateStatusFlags(newByte);
                CurrentCycle += 7;
                break;
            }
            default:
            {
                throw new Exception($"Invalid instruction {opcode:X2})");
            }
        }
    }

    public void CheckIfPageCrossed(ushort oldAddr, ushort newAddr)
    {
        if ((newAddr & 0xFF00) != (oldAddr & 0xFF00))
        {
            CurrentCycle += 1;
        }
    }

    private ushort ReadAddrIndirectX()
    {
        var zeroPageAddr = (byte)(_bus.ReadNextByte() + X);
        var lsbAddr = _bus.ReadMemoryByAddr(zeroPageAddr);
        var msbAddr = _bus.ReadMemoryByAddr((byte) (zeroPageAddr + 0x1));
        return (ushort) ((msbAddr << 8) | lsbAddr);
    }
    
    private byte ReadByteZeroPageX()
    {
        return _bus.ReadMemoryByAddr((byte) (_bus.ReadNextByte() + X));
    }

    private byte ReadByteAbsolute()
    {
        return _bus.ReadMemoryByAddr(_bus.ReadAddrAbsolute());
    }

    private byte ReadByteIndirectX()
    {
        var addr = ReadAddrIndirectX();
        return _bus.ReadMemoryByAddr(addr);
    }

    private byte ReadByteAbsoluteY()
    {
        var addr = _bus.ReadAddrAbsolute();
        var absoluteY = (ushort) (addr + Y);
        CheckIfPageCrossed(addr, absoluteY);
        return _bus.ReadMemoryByAddr(absoluteY);
    }
    
    private byte ReadByteAbsoluteX()
    {
        var addr = _bus.ReadAddrAbsolute();
        var absoluteX = (ushort) (addr + X);
        CheckIfPageCrossed(addr, absoluteX);
        return _bus.ReadMemoryByAddr(absoluteX);
    }

    private ushort ReadAddrIndirectY()
    {
        var zeroPageAddr = _bus.ReadNextByte();
        var firstAddr = _bus.ReadMemoryByAddr(zeroPageAddr);
        var secondAddr = _bus.ReadMemoryByAddr((byte) (zeroPageAddr + 1));
        var finalAddr = (ushort) (firstAddr | (secondAddr << 8));
        CheckIfPageCrossed(finalAddr, (ushort) (finalAddr + Y));
        return (ushort) (finalAddr + Y);
    }

    private byte ReadByteIndirectY()
    {
        var addr = ReadAddrIndirectY();
        var memValue = _bus.ReadMemoryByAddr(addr);
        return memValue;
    }

    //ADC
    public void AddWithCarry(byte operand)
    {
        var hasCarryFlag = (StatusFlags & StatusFlagsConstants.CARRY) != 0;
        var result = (ushort) (Accumulator + operand);
        
        if (hasCarryFlag)
            result += 1;
        
        AssignFlag(StatusFlagsConstants.CARRY, (result & 0x100) != 0);
        AssignFlag(StatusFlagsConstants.OVERFLOW, ((Accumulator ^ result) & (operand ^ result) & 0x80) != 0);
        
        Accumulator = (byte) result;
        UpdateStatusFlags(Accumulator);
    }
}