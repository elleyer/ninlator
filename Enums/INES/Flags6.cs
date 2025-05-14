namespace Emulator.Enums.INES;

[Flags]
public enum Flags6 : byte
{
    None = 0,
    NameTableArrangement = 1,
    HasPersistedMemory = 2,
    HasTrainer = 4,
    AlternativeNametableLayout = 8
}