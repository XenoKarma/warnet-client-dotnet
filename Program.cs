using System.Text.Json;

namespace WarnetClient;

static class Program
{
    public static Config AppConfig = null!;

    [STAThread]
    static void Main()
    {
        var json = File.ReadAllText("config.json");
        AppConfig = JsonSerializer.Deserialize<Config>(json)
            ?? throw new Exception("Gagal membaca config.json");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class Config
{
    public string pcNumber { get; set; } = "PC-01";
    public string serverUrl { get; set; } = "http://localhost:5000";
    public bool debug { get; set; } = false;
}
