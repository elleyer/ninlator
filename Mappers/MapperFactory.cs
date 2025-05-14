namespace Emulator.Mappers;

public static class MapperFactory
{
    public static IMapper CreateById(byte id)
    {
        switch (id)
        {
            case 0:
                return new Mapper0();
            case 2:
                return new Mapper2();
            default:
                throw new Exception($"Invalid mapper id {id}");
        }
    }
}