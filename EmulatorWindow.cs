using System.Drawing;
using Emulator.Logs;
using Emulator.Shaders;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Emulator;

public class EmulatorWindow : GameWindow
{
    private Shader _shader;
    private bool _romSelected;
    
    private readonly float[] _vertices =
    {
        -1f, -1f, 0.0f, 0.0f, 1f,
        -1f, 1f, 0.0f, 0.0f, 0f,
        1f, 1f, 0.0f, 1f, 0f,
        1f, -1f, 0.0f, 1f, 1f
    };
        
    private readonly uint[] _indices = {  // note that we start from 0!
        0, 1, 3,   // first triangle
        1, 2, 3    // second triangle
    };

    private int _elementBufferObject;
    private int _vertexBufferObject;
    private int _vertexArrayObject;
    
    private Texture _renderTexture;
    private Emulator _emulator;
    
    public EmulatorWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        Size = new Vector2i(1280, 720);
        _emulator = new Emulator();
        _renderTexture = Texture.GenerateTexture(256, 240);
        _shader = new Shader(@"Shaders\Shader.vert", 
            @"Shaders\Shader.frag");
        _shader.Use();
    }

    private void InitializeRom(string romName)
    {
        _emulator.Initialize(@$"Resources\{romName}.nes", @"Resources\palette.pal");
        _romSelected = true;
    }

    protected override void OnUnload()
    {
        _shader.Dispose();
        base.OnUnload();
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(Color.Black);
        
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);

        _vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

        _elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);
        
        var vertexLocation = GL.GetAttribLocation(_shader.Handle, "aPosition");
        GL.EnableVertexAttribArray(vertexLocation);
        GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        
        var texCoordLocation = GL.GetAttribLocation(_shader.Handle,"aTexCoord");
        GL.EnableVertexAttribArray(texCoordLocation);
        GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        
        if (!_romSelected)
            return;
        
        if (!_emulator.Ppu.IsNewFrameReady) 
            return;
        
        _shader.Use();
        _renderTexture.FillTextureData(_emulator.Ppu.GetPixelsData());
        _renderTexture.Use(TextureUnit.Texture0);
            
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length,
            DrawElementsType.UnsignedInt, 0);
        SwapBuffers();
    }
    
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        
        if (KeyboardState.IsKeyPressed(Keys.F1))
        {
            Emulator.Logger?.Dispose();
            var shouldLog = !Emulator.IsLoggingEnabled;
            if (shouldLog)
                Emulator.Logger = new Logger();

            Emulator.IsLoggingEnabled = shouldLog;
        }
        
        Title = (1 / args.Time).ToString("F4");
        if (Emulator.IsLoggingEnabled)
            Title += " [Recording]";
        
        if (!_romSelected)
        {
            if (KeyboardState.IsKeyPressed(Keys.D1))
                InitializeRom("contra");
            if (KeyboardState.IsKeyPressed(Keys.D2))
                InitializeRom("rom");
            if (KeyboardState.IsKeyPressed(Keys.D3))
                InitializeRom("bike");
            if (KeyboardState.IsKeyPressed(Keys.D4))
                InitializeRom("duck");
            if (KeyboardState.IsKeyPressed(Keys.D5))
                InitializeRom("kong");
            if (KeyboardState.IsKeyPressed(Keys.D6))
                InitializeRom("mario");
            if (KeyboardState.IsKeyPressed(Keys.D7))
                InitializeRom("ducktales");
            return;
        }
        _emulator.UpdateInput(KeyboardState);
        _emulator.Update(args.Time);
    }
}