namespace Emulator;

public class SystemColor
{
    public byte R { get; private set; }
    public byte G { get; private set; }
    public byte B { get; private set; }

    public SystemColor(byte r, byte g, byte b)
    {
        R = r;
        B = b;
        G = g;
    }
}