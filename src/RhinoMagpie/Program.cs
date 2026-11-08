using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Win32;

namespace RhinoMagpie;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool diagnostic = args.Contains("--diagnose");
        try
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configPath = Path.Combine(profile, ".config", "rhino9-magpie", "settings.json");
            string? rhinoOverride = null;
            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--diagnose": break;
                    case "--config" when index + 1 < args.Length: configPath = args[++index]; break;
                    case "--rhino" when index + 1 < args.Length: rhinoOverride = args[++index]; break;
                    default: throw new ArgumentException("Usage: RhinoMagpie.exe [--diagnose] [--config path] [--rhino path]");
                }
            }
            if (!File.Exists(configPath))
                throw new ArgumentException("Install the Rhino 9 AI plugin in Magpie and select a model first.\n请先在 Magpie 安装 Rhino 9 AI 插件并选择模型。");
            var config = BridgeConfig.Parse(File.ReadAllText(configPath));
            string rhino = FindRhino(rhinoOverride);
            string claude = FindClaude(profile);
            ValidateGateway(config).GetAwaiter().GetResult();
            if (diagnostic)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    status = "ready",
                    rhino,
                    claude,
                    model = config.Model,
                    gateway = config.BaseUrl,
                    instruction = "AI panel: Claude / Opus (latest). Launch with RhinoMagpie.exe.",
                }));
                return 0;
            }
            string claudeDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rhino9Magpie", "Claude");
            Directory.CreateDirectory(claudeDirectory);
            var start = new ProcessStartInfo(rhino)
            {
                UseShellExecute = false,
                Arguments = "/nosplash /runscript=\"_AI\"",
            };
            var environment = config.CreateEnvironment(start.Environment, claudeDirectory);
            start.Environment.Clear();
            foreach (var entry in environment) start.Environment[entry.Key] = entry.Value;
            _ = Process.Start(start) ?? throw new InvalidOperationException("Windows could not start Rhino.");
            return 0;
        }
        catch (Exception exception)
        {
            string message = exception is HttpRequestException or TaskCanceledException
                ? "Cannot reach the local Magpie gateway. Start Magpie and try again.\n无法连接本机 Magpie，请启动 Magpie 后重试。"
                : exception is ArgumentException or InvalidOperationException
                    ? exception.Message
                    : "Could not read the configuration or launch Rhino. Check file access and installation paths.";
            if (diagnostic) Console.Error.WriteLine(message);
            else MessageBox.Show(message, "Rhino 9 + Magpie", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static string FindRhino(string? supplied)
    {
        if (supplied is not null)
        {
            if (!File.Exists(supplied)) throw new ArgumentException("The supplied Rhino executable does not exist.");
            return Path.GetFullPath(supplied);
        }
        using var install = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\McNeel\Rhinoceros\9.0\Install");
        string? path = install?.GetValue("ExePath") as string;
        path ??= Path.Combine(install?.GetValue("Path") as string ?? "", "Rhino.exe");
        if (File.Exists(path)) return path;
        throw new ArgumentException("Rhino 9 was not found. Install Rhino 9, or pass --rhino followed by Rhino.exe's path.");
    }

    private static string FindClaude(string profile)
    {
        return ClaudeLocator.Find(profile,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            ?? throw new ArgumentException("Claude Code is required by Rhino's built-in AI agent. Install it with the official Windows installer, then run this launcher again.\nRhino 原生代理需要 Claude Code。请先安装，再运行启动器。");
    }

    private static async Task ValidateGateway(BridgeConfig config)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        using var helloResponse = await client.GetAsync(config.BaseUrl + "/api/hello");
        helloResponse.EnsureSuccessStatusCode();
        using var hello = JsonDocument.Parse(await helloResponse.Content.ReadAsStringAsync());
        if (!hello.RootElement.TryGetProperty("name", out var name) || name.GetString() != "magpie")
            throw new ArgumentException("The configured endpoint is not a Magpie gateway.");
        using var modelsResponse = await client.GetAsync(config.BaseUrl + "/v1/models");
        modelsResponse.EnsureSuccessStatusCode();
        using var models = JsonDocument.Parse(await modelsResponse.Content.ReadAsStringAsync());
        if (!models.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array
            || !data.EnumerateArray().Any(model => model.TryGetProperty("id", out var id) && id.GetString() == config.Model))
            throw new ArgumentException("The selected model is no longer available. Choose another model in Magpie.");
    }
}
