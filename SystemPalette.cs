namespace Emulator;

public class SystemPalette
{
    private const byte ColorsAmount = 64;
    private readonly SystemColor[] _colorsData = new SystemColor[ColorsAmount];

    public SystemPalette(byte[] data)
    {
        var arrayIndex = -1;
        for (var i = 0; i < 64; i++)
        {
            var r = data[++arrayIndex];
            var g = data[++arrayIndex];
            var b = data[++arrayIndex];
            _colorsData[i] = new SystemColor(r, g, b);
        }
    }

    public SystemColor GetColorByIndex(byte index)
    {
        return _colorsData[index];
    }

    public static SystemPalette LoadFromFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open);
        using var reader = new BinaryReader(stream);
        var data = reader.ReadBytes(ColorsAmount * 3);
        return new SystemPalette(data);
    }
}