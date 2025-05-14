using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Emulator.Controller;

public class StandardController
{
    private byte _dataLine;
    private KeyboardState? _keyboardState;

    public byte ReadNextButton()
    {
        var value = _dataLine & 0x1;
        _dataLine >>= 1;
        return (byte) value;
    }

    public void CheckButtons(bool state)
    {
        if (!state || _keyboardState == null)
            return;

        if (_keyboardState.IsKeyDown(Keys.Down))
            PressButton(ButtonType.Down);

        if (_keyboardState.IsKeyDown(Keys.Up))
            PressButton(ButtonType.Up);
        
        if (_keyboardState.IsKeyDown(Keys.Left))
            PressButton(ButtonType.Left);

        if (_keyboardState.IsKeyDown(Keys.Right))
            PressButton(ButtonType.Right);
        
        if (_keyboardState.IsKeyDown(Keys.Z))
            PressButton(ButtonType.A);
        
        if (_keyboardState.IsKeyDown(Keys.X))
            PressButton(ButtonType.B);

        if (_keyboardState.IsKeyDown(Keys.Enter))
            PressButton(ButtonType.Start);

        if (_keyboardState.IsKeyDown(Keys.Space))
            PressButton(ButtonType.Select);
    }

    public void UpdateInputData(KeyboardState state) => _keyboardState = state;
    
    private void PressButton(ButtonType buttonType) => _dataLine |= (byte) buttonType;
}