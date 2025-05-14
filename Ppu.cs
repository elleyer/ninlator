using Emulator.Constants;
using Emulator.Enums.INES;

namespace Emulator;

public class Ppu
{
    private const ushort NameTableMask = 0x3FF;
    private const ushort PaletteTableMask = 0x1F;
    private const ushort AttrTableOffset = 0x23C0;

    private const int Width = 256;
    private const int Height = 240;
    
    private const int TileSize = 8;
    private const int TilesPerAttributeBlock = 4;

    private const int AttributeTableBlockSize = TileSize * TilesPerAttributeBlock;

    public int ScreenX { get; private set; }
    public int ScreenY { get; private set; }

    public const int VblankOffset = 82182;
    public const int FramePeriod = 89342;

    public long CurrentCycle { get; set; }
    public long NextVBlankCycle;

    public bool IsNewFrameReady;

    public readonly byte[] PpuRegisters = new byte[8];

    private readonly byte[] _nameTable0 = new byte[0x0400];
    private readonly byte[] _nameTable1 = new byte[0x0400];

    private readonly byte[] _usedPalette = new byte[32];

    private byte[] _oam = new byte[256];
    private byte[] _internalOam = new byte[32];
        
    private byte _internalReadBuffer;

    private ushort _v;
    private ushort T { get; set; }
    
    //Represents Fine X Scroll.
    private byte _x;

    private bool _w;
    private bool _ppuStatusVBlank;

    private readonly Rom _rom;
    private readonly SystemPalette _systemPalette;

    private byte[] _pixels;

    public Ppu(Rom rom, SystemPalette systemPalette)
    {
        _rom = rom;
        _systemPalette = systemPalette;
        _pixels = new byte[Width * Height * 4];
    }

    public void CatchUpToCpuCycle(long cycle)
    {
        //The ppu's frequency is 3 times higher that the cpu's.
        var targetCycle = cycle * 3;
        //Do work while our current cycle is lower than the target cycle.
        
        //The value written to PPUCTRL ($2000) controls whether the background and sprites
        //use the first pattern table ($0000-$0FFF) or the second pattern table ($1000-$1FFF).
        while (CurrentCycle < targetCycle)
        {
            //Current scanline
            if (ScreenY < 240)
            {
                if (ScreenX == 340)
                {
                    for (var i = 0; i < 32; i++)
                    {
                        _internalOam[i] = 0xFF;
                    }
                    
                    var spritesFound = 0;
                    for (var i = 0; i < 64; i++)
                    {
                        var cmp = ScreenY - _oam[i * 4];
                        if (cmp >= 0 && cmp < GetCurrentSpriteHeight())
                        {
                            _internalOam[spritesFound * 4] = _oam[i * 4];
                            _internalOam[spritesFound * 4 + 1] = _oam[i * 4 + 1];
                            _internalOam[spritesFound * 4 + 2] = _oam[i * 4 + 2];
                            _internalOam[spritesFound * 4 + 3] = _oam[i * 4 + 3];
                            spritesFound++;
                        }
                        if (spritesFound == 8)
                            break;
                    }
                }
                
                //Scanline cycle
                if (ScreenX < 256)
                {
                    if (IsRenderingEnabled)
                    {
                        var fineY = (_v & 0x7000) >> 12;
                        var bkgPatternTableIndex = ReadVideoMemory((ushort)((_v & 0x0FFF) | 0x2000));
                        var attrTableBaseAddr = (ushort) ((_v & 0xC00) | AttrTableOffset);
                        
                        //Check if coarse X == 31.
                        if ((_v & 0x1F) == 0x1F)
                        {
                            //Set coarse X to 0;
                            _v &= unchecked((ushort)~0x1F);
                            //Flip nametable bit
                            _v ^= 0x400;
                        }
                        else
                            _v += 1;

                        var bkgPatternTableAddr = ((PpuRegisters[PpuRegisterConstants.PPUCTRL] >> 4) & 0x1) << 12;
                        var patternTablePart = bkgPatternTableIndex << 4;
                        var addr = (ushort)(fineY | patternTablePart | bkgPatternTableAddr);
                        var addrSecond = (ushort) (addr + 8);
                        
                        var firstPlane = ReadVideoMemory(addr);
                        var secondPlane = ReadVideoMemory(addrSecond);
                        
                        //Draw 8 pixels at the same time
                        for (var i = 0; i < 8; i++)
                        {
                            var pixelIndex = (ScreenY * Width + ScreenX) * 4;

                            var yBlockIndex = ScreenY / AttributeTableBlockSize;
                            var xBlockIndex = ScreenX / AttributeTableBlockSize;

                            var blockXOffset = ScreenX - xBlockIndex * AttributeTableBlockSize;
                            var blockYOffset = ScreenY - yBlockIndex * AttributeTableBlockSize;

                            var attrTableTileAddr = attrTableBaseAddr + 8 * yBlockIndex + xBlockIndex;
                            var attrTableTileByte = ReadVideoMemory((ushort) attrTableTileAddr);

                            var blockQuadrant = 0;
                            
                            if (blockXOffset < TileSize * 2 && blockYOffset < TileSize * 2)
                                blockQuadrant = attrTableTileByte;
                            
                            else if (blockXOffset >= TileSize * 2 && blockYOffset >= TileSize * 2)
                                blockQuadrant = attrTableTileByte >> 6;
                            
                            else if (blockXOffset < TileSize * 2 && blockYOffset >= TileSize * 2)
                                blockQuadrant = attrTableTileByte >> 4;
                            
                            else if (blockXOffset >= TileSize * 2 && blockYOffset < TileSize * 2)
                                blockQuadrant = attrTableTileByte >> 2;
                            
                            var bitOnFirstPlane = ((1 << (7 - i)) & firstPlane) >> (7 - i);
                            var bitOnSecondPlane = ((1 << (7 - i)) & secondPlane) >> (7 - i);
                            var backgroundTileData = (bitOnSecondPlane << 1) | bitOnFirstPlane;
                            var backgroundPaletteIndex = GetBackgroundPaletteColorIndex
                            ((byte)(blockQuadrant & 0x3), (byte) backgroundTileData);
                            
                            var backgroundColor = _systemPalette.GetColorByIndex(backgroundPaletteIndex);

                            _pixels[pixelIndex] = backgroundColor.R;
                            _pixels[pixelIndex + 1] = backgroundColor.G;
                            _pixels[pixelIndex + 2] = backgroundColor.B;
                            _pixels[pixelIndex + 3] = 255;
                            
                            //Evaluate 8 sprites. 
                            for (var j = 0; j < 8; j += 1)
                            {
                                var spriteIndexNumber = _internalOam[j * 4 + 1];
                                if (spriteIndexNumber == 0xFF)
                                    continue;
                                
                                //Pattern table selected through bit 3 of $2000
                                var spritePatternTableAddr = ((PpuRegisters[PpuRegisterConstants.PPUCTRL] >> 3) & 0x1) << 12;
                                var largeSpriteMode = GetCurrentSpriteHeight() == 16;

                                if (largeSpriteMode)
                                {
                                    spritePatternTableAddr = spriteIndexNumber % 2 == 0 ? 0x0 : 0x1000;
                                    spriteIndexNumber &= unchecked((byte)~0x1);
                                }
                                
                                var yLineToDraw = ScreenY - _internalOam[j * 4] - 1;
                                if ((0x80 & _internalOam[j * 4 + 2]) != 0)
                                    yLineToDraw = GetCurrentSpriteHeight() - 1 - yLineToDraw;

                                if (yLineToDraw > 7)
                                {
                                    spritePatternTableAddr += 8;
                                }

                                var addr1 = (ushort)(spritePatternTableAddr + 16 * spriteIndexNumber + yLineToDraw);
                                var addr2 = (ushort) (spritePatternTableAddr + 16 * spriteIndexNumber + yLineToDraw + 8);
                                var firstSpritePlaneRow = ReadVideoMemory(addr1);
                                var secondSpritePlaneRow = ReadVideoMemory(addr2);
                                
                                //Then check for the X coordinate.
                                var diff = ScreenX - _internalOam[j * 4 + 3];
                                if (diff is >= 0 and < 8)
                                {
                                    var bitToCheck = (0x40 & _internalOam[j * 4 + 2]) == 0
                                        ? 1 << (7 - diff)
                                        : 1 << diff;
                                    
                                    var firstPlaneBit = (bitToCheck & firstSpritePlaneRow) == 0 ? 0 : 1;
                                    var secondPlaneBit = (bitToCheck & secondSpritePlaneRow) == 0 ? 0 : 1;

                                    var tileData = (byte)((secondPlaneBit << 1) | firstPlaneBit);

                                    if (tileData != 0)
                                    {
                                        //Sprite palette index is defined in its third byte's 0 and 1 bits.
                                        var spriteColorIndex = GetSpritePaletteColorIndex((byte) (_internalOam[j * 4 + 2] & 0x3), 
                                            tileData);
                                        var spriteColor = _systemPalette.GetColorByIndex(spriteColorIndex);
                                        
                                        //Check for the priority bit.
                                        if ((_internalOam[j * 4 + 2] & 0x20) != 0)
                                        {
                                            //If the background pixel is not transparent - leave its color as it is.
                                            if (backgroundTileData != 0)
                                                break;
                                        }
                                    
                                        _pixels[pixelIndex] = spriteColor.R;
                                        _pixels[pixelIndex + 1] = spriteColor.G;
                                        _pixels[pixelIndex + 2] = spriteColor.B;
                                        _pixels[pixelIndex + 3] = 255;
                                        
                                        //Priority between sprites is determined by their address inside OAM.
                                        //So to have a sprite displayed in front of another sprite in a scanline,
                                        //the sprite data that occurs first will overlap any other sprites after it.
                                        break;
                                    }
                                }
                            } 
                            ScreenX++;
                        }
                    }
                    else
                    {
                        ScreenX += 8;
                    }
                    CurrentCycle += 8;
                }
                else
                {
                    if (ScreenX == 256 && IsRenderingEnabled)
                    {
                        if ((_v & 0x7000) != 0x7000) // if fine Y < 7
                            _v += 0x1000; // increment fine Y
                        else
                        {
                            _v &= unchecked((ushort)~0x7000); // fine Y = 0
                            var y = (_v & 0x03E0) >> 5; // let y = coarse Y
                            if (y == 29)
                            {
                                y = 0; // coarse Y = 0
                                _v ^= 0x0800; // switch vertical nametable
                            }
                            else if (y == 31)
                                y = 0; // coarse Y = 0, nametable not switched
                            else
                                y += 1; // increment coarse Y

                            _v = (ushort)((_v & ~0x03E0) | (y << 5)); // put coarse Y back into v
                        }
                    }

                    if (ScreenX == 257 && IsRenderingEnabled)
                    {
                        //v: ....A.. ...BCDEF <- t: ....A.. ...BCDEF
                        _v = (ushort) ((_v & ~0x41f) | (T & 0x41f));
                    }
                    
                    CurrentCycle++;
                    ScreenX++;
                    
                    if (ScreenX == 341)
                    {
                        ScreenY++;
                        ScreenX = 0;
                    }
                }
            }
            else
            {
                if (ScreenY == 240 && ScreenX == 0)
                {
                    IsNewFrameReady = true;
                }

                if (ScreenY == 241 && ScreenX == 1)
                { 
                    CalculateNextVBlankCycle();
                    _ppuStatusVBlank = true;
                }

                if (ScreenY == 261 && ScreenX == 1)
                {
                    _ppuStatusVBlank = false;
                }

                if (IsRenderingEnabled)
                {
                    if (ScreenY == 261 && ScreenX == 257)
                    {
                        //v: ....A.. ...BCDEF <- t: ....A.. ...BCDEF
                        _v = (ushort) ((_v & ~0x41f) | (T & 0x41f));
                    }
                
                    if (ScreenX is >= 280 and <= 304 && ScreenY == 261)
                    {
                        //v: GHIA.BC DEF..... <- t: GHIA.BC DEF.....
                        _v = (ushort) ((_v & ~0x7BE0) | (T & 0x7BE0));
                    }  
                }
                
                ScreenX++;
                CurrentCycle++;
                
                if (ScreenX == 341)
                {
                    ScreenY++;
                    ScreenX = 0;
                    if (ScreenY == 262)
                    {
                        ScreenY = 0;
                    }
                }
            }
        }
    }

    //Set next VBlank target cycle to catch a non-maskable interrupt.
    private void CalculateNextVBlankCycle()
    {
        NextVBlankCycle += FramePeriod;
    }

    public byte[] GetPixelsData()
    {
        IsNewFrameReady = false;
        return _pixels;
    }

    public void WriteRegister(byte index, byte data)
    {
        switch (index)
        {
            case PpuRegisterConstants.PPUADDR:
            {
                if (!_w)
                {
                    T &= 0x00FF;
                    T = (ushort)(((data << 8) | T) & 0x3FFF);
                }
                else
                {
                    T &= 0xFF00;
                    T |= data;
                    _v = T;
                }
                _w = !_w;
                break;
            }
            case PpuRegisterConstants.PPUMASK:
            {
                PpuRegisters[PpuRegisterConstants.PPUMASK] = data;
                break;
            }
            case PpuRegisterConstants.OAMDATA:
            {
                _oam[PpuRegisters[PpuRegisterConstants.OAMADDR]] = data;
                PpuRegisters[PpuRegisterConstants.OAMADDR]++;
                break;
            }
            case PpuRegisterConstants.PPUSCROLL:
            {
                if (!_w)
                {
                    T = (ushort)(data >> 3 | (T & ~0x1f));
                     _x = (byte)(data & 0x7);
                    _w = true;
                }
                else
                {
                    //t: FGH..AB CDE..... <- d: ABCDEFGH
                    var firstPart = T & 0x1f;
                    var secondPart = T & 0xC00;
                    var fgh = (data & 0x7) << 12;
                    var abcde = ((data >> 3) & 0x1f) << 5;
                    T = (ushort) (firstPart | secondPart | fgh | abcde);
                    _w = false;
                }
                break;
            }
            case PpuRegisterConstants.PPUDATA:
            {
                WriteVideoMemory(_v, data);
                _v += (ushort)((PpuRegisters[PpuRegisterConstants.PPUCTRL] & 0x4) == 0 ? 1 : 32);
                break;
            }
            case PpuRegisterConstants.PPUCTRL:
            {
                //t: ...GH.. ........ <- d: ......GH
                PpuRegisters[PpuRegisterConstants.PPUCTRL] = data;
                T = (ushort)(((data & 0x3) << 10) | (T & ~0xC00));
                break;
            }
        }

        PpuRegisters[index] = data;
    }

    public byte ReadRegister(byte index)
    {
        switch (index)
        {
            case PpuRegisterConstants.PPUDATA:
            {
                var prevBuffer = _internalReadBuffer;
                _internalReadBuffer = ReadVideoMemory(_v);
                _v += (ushort)((PpuRegisters[PpuRegisterConstants.PPUCTRL] & 0x4) == 0 ? 1 : 32);
                return prevBuffer;
            }
            case PpuRegisterConstants.PPUSTATUS:
            {
                _w = false;
                return _ppuStatusVBlank ? (byte) 0x80 : (byte) 0;
            }
            case PpuRegisterConstants.OAMDATA:
            {
                if (_ppuStatusVBlank)
                {
                    return _oam[PpuRegisters[PpuRegisterConstants.OAMADDR]];
                }
                return 0;
            }
            default:
                return unchecked((byte)-1);
        }
    }

    public byte ReadVideoMemory(ushort addr)
    {
        switch (addr)
        {
            case <= 0x1FFF:
                return _rom.ReadChr(addr);
            case <= 0x23FF:
                return _nameTable0[addr & NameTableMask];
            case <= 0x27FF:
            {
                return _rom.RomMirroringType == MirroringType.Horizontal 
                    ? _nameTable0[addr & NameTableMask] 
                    : _nameTable1[addr & NameTableMask];
            }
            case <= 0x2BFF:
            {
                return _rom.RomMirroringType == MirroringType.Horizontal 
                    ? _nameTable1[addr & NameTableMask] 
                    : _nameTable0[addr & NameTableMask];
            }
            case <= 0x2FFF:
                return _nameTable1[addr & NameTableMask];
            case >= 0x3F00 and <= 0x3FFF:
                return _usedPalette[addr & PaletteTableMask];
        }
        return 0;
    }

    public void WriteVideoMemory(ushort addr, byte data)
    {
        switch (addr)
        {
            case <= 0x1FFF:
            {
                _rom.WriteChrRam(addr, data);
                break;
            }
            case <= 0x23FF:
            {
                _nameTable0[addr & NameTableMask] = data;
                break;
            }
            case <= 0x27FF:
            {
                if (_rom.RomMirroringType == MirroringType.Horizontal)
                    _nameTable0[addr & NameTableMask] = data;
                else
                    _nameTable1[addr & NameTableMask] = data;
                break;
            }
            case <= 0x2BFF:
            {
                if (_rom.RomMirroringType == MirroringType.Horizontal)
                    _nameTable1[addr & NameTableMask] = data;
                else
                    _nameTable0[addr & NameTableMask] = data;
                break;
            }
            case <= 0x2FFF:
            {
                _nameTable1[addr & NameTableMask] = data;
                break;
            }
            case >= 0x3F00 and <= 0x3FFF:
            {
                _usedPalette[addr & PaletteTableMask] = data;
                break;
            }
        }
    }
    
    //Accept 2-bit merged value of the both tile planes
    private byte GetSpritePaletteColorIndex(byte paletteIndex, byte tileData)
    {
        var baseAddr = 0x3F10 + 4 * paletteIndex;
        return ReadVideoMemory((ushort) (baseAddr + tileData));
    }
    
    //Accept 2-bit merged value of the both tile planes
    private byte GetBackgroundPaletteColorIndex(byte paletteIndex, byte tileData)
    {
        var baseAddr = 0x3F00 + 4 * paletteIndex;
        return ReadVideoMemory((ushort) (baseAddr + tileData));
    }

    private byte GetCurrentSpriteHeight() => (byte) ((PpuRegisters[PpuRegisterConstants.PPUCTRL] & 0x20) == 0 ? 8 : 16);
    
    private bool ShowBackground => (PpuRegisters[PpuRegisterConstants.PPUMASK] & 8) != 0;
    private bool ShowSprites => (PpuRegisters[PpuRegisterConstants.PPUMASK] & 16) != 0;

    private bool IsRenderingEnabled => ShowBackground && ShowSprites;
}