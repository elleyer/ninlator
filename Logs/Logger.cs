namespace Emulator.Logs;

public class Logger : IDisposable
{
    private const string AppName = "NEStalgia";
    private const string FileExtension = "log";

    private StreamWriter _streamWriter;

    public Logger()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            $"{AppName}");
        
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        
        var stream = new FileStream($"{path}/{DateTime.Now:yyMMdd_hhmmss}.{FileExtension}", FileMode.OpenOrCreate);
        _streamWriter = new StreamWriter(stream);
    }
    
    public void Log(string data)
    {
        _streamWriter.WriteLine(data);
    }

    public void Dispose()
    {
        _streamWriter.Dispose();
    }
}