using OpenTK.Windowing.Desktop;

namespace Emulator;

internal class Program
{
    public static EmulatorWindow Window;
    static void Main(string[] args)
    {
        Window = new EmulatorWindow(new GameWindowSettings
        {
            UpdateFrequency = 60
        }, NativeWindowSettings.Default);
        Window.Run();
    }
}