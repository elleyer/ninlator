namespace Emulator.Constants;

public class StatusFlagsConstants
{
    public const byte CARRY = 1;
    public const byte ZERO = 1 << 1;
    public const byte INTERRUPT_DISABLE = 1 << 2;
    public const byte DECIMAL = 1 << 3;
    public const byte OVERFLOW = 1 << 6;
    public const byte NEGATIVE = 1 << 7;
}