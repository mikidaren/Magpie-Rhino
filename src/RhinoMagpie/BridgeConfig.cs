using System.Text.Json;

namespace RhinoMagpie;

public sealed record BridgeConfig(string Model, string BaseUrl, string ApiKey)
{
    public static BridgeConfig Parse(string json)
    {
        BridgeConfig config;
        try
        {
            config = JsonSerializer.Deserialize<BridgeConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("The Magpie connection is empty.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("The Magpie connection file is not valid JSON.");
        }
        if (string.IsNullOrWhiteSpace(config.Model) || string.IsNullOrWhiteSpace(config.ApiKey)
            || config.Model.Any(char.IsControl) || config.ApiKey.Any(char.IsControl))
            throw new ArgumentException("Choose a model in Magpie's Rhino 9 AI row first.");
        if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != "http" || !uri.IsLoopback
            || uri.Host is not ("127.0.0.1" or "localhost" or "[::1]")
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0
            || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new ArgumentException("This release requires a local Magpie HTTP gateway root URL, without /v1.");
        return config with { BaseUrl = uri.GetLeftPart(UriPartial.Authority) };
    }

    public Dictionary<string, string?> CreateEnvironment(
        IEnumerable<KeyValuePair<string, string?>> parent, string claudeDirectory)
    {
        var environment = new Dictionary<string, string?>(parent, StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[]
        {
            "ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "ANTHROPIC_CUSTOM_HEADERS",
            "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY",
            "ANTHROPIC_SMALL_FAST_MODEL", "ANTHROPIC_MODEL", "CLAUDE_CODE_SUBAGENT_MODEL",
        }) environment.Remove(name);
        environment["ANTHROPIC_BASE_URL"] = BaseUrl;
        environment["ANTHROPIC_AUTH_TOKEN"] = ApiKey;
        environment["ANTHROPIC_MODEL"] = Model;
        environment["ANTHROPIC_DEFAULT_OPUS_MODEL"] = Model;
        environment["ANTHROPIC_DEFAULT_SONNET_MODEL"] = Model;
        environment["ANTHROPIC_DEFAULT_HAIKU_MODEL"] = Model;
        environment["CLAUDE_CODE_SUBAGENT_MODEL"] = Model;
        environment["CLAUDE_CONFIG_DIR"] = claudeDirectory;
        environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
        return environment;
    }
}
