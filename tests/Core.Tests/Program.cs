using RhinoMagpie;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine($"PASS {name}");
    passed++;
}
void Reject(string json)
{
    try { BridgeConfig.Parse(json); }
    catch (ArgumentException) { passed++; return; }
    throw new Exception($"Accepted invalid config: {json}");
}

var config = BridgeConfig.Parse("""{"model":"vendor/model/name","baseUrl":"http://127.0.0.1:4567","apiKey":"test-secret"}""");
var original = new Dictionary<string, string?>
{
    ["ANTHROPIC_API_KEY"] = "personal",
    ["CLAUDE_CODE_OAUTH_TOKEN"] = "personal",
    ["CLAUDE_CODE_USE_BEDROCK"] = "1",
    ["OTHER_SETTING"] = "preserve",
};
var environment = config.CreateEnvironment(original, @"C:\space path\isolated");
Check(environment["ANTHROPIC_BASE_URL"] == "http://127.0.0.1:4567", "custom gateway port");
Check(environment["ANTHROPIC_AUTH_TOKEN"] == "test-secret", "gateway key");
Check(environment["ANTHROPIC_DEFAULT_OPUS_MODEL"] == "vendor/model/name", "opus alias");
Check(environment["ANTHROPIC_DEFAULT_SONNET_MODEL"] == config.Model, "sonnet alias");
Check(environment["ANTHROPIC_DEFAULT_HAIKU_MODEL"] == config.Model, "haiku alias");
Check(environment["CLAUDE_CONFIG_DIR"] == @"C:\space path\isolated", "isolated Claude home");
Check(!environment.ContainsKey("ANTHROPIC_API_KEY"), "no inherited API credential");
Check(!environment.ContainsKey("CLAUDE_CODE_OAUTH_TOKEN"), "no inherited OAuth credential");
Check(!environment.ContainsKey("CLAUDE_CODE_USE_BEDROCK"), "no inherited backend override");
Check(environment["OTHER_SETTING"] == "preserve", "unrelated environment preserved");
Check(original["ANTHROPIC_API_KEY"] == "personal", "parent environment unchanged");
Reject("{}");
Reject("""{"model":"","baseUrl":"http://127.0.0.1:3425","apiKey":"key"}""");
Reject("""{"model":"model","baseUrl":"https://remote.example","apiKey":"key"}""");
Reject("""{"model":"model","baseUrl":"http://127.0.0.1:3425/v1","apiKey":"key"}""");
Reject("""{"model":"model","baseUrl":"http://user:pass@127.0.0.1:3425","apiKey":"key"}""");
Reject("""{"model":"model","baseUrl":"http://127.0.0.1:3425?key=secret","apiKey":"key"}""");
Reject("""{"model":"model","baseUrl":"http://127.0.0.1:3425","apiKey":"key\nbad"}""");
string fixture = Path.Combine(Path.GetTempPath(), "rhino-magpie-tests-" + Guid.NewGuid());
string profile = Path.Combine(fixture, "user");
string roaming = Path.Combine(fixture, "roaming");
string local = Path.Combine(fixture, "local");
try
{
    Check(ClaudeLocator.Find(profile, roaming, local) is null, "missing Claude");
    string native = Path.Combine(profile, ".local", "bin", "claude.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(native)!);
    File.WriteAllText(native, "");
    Check(ClaudeLocator.Find(profile, roaming, local) == native, "native Claude");
    string desktop = Path.Combine(roaming, "Claude", "claude-code", "2.1", "claude.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(desktop)!);
    File.WriteAllText(desktop, "");
    Check(ClaudeLocator.Find(profile, roaming, local) == desktop, "desktop precedence");
    string store = Path.Combine(local, "Packages", "Claude_test", "LocalCache", "Roaming", "Claude", "claude-code", "2.1", "claude.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(store)!);
    File.WriteAllText(store, "");
    Check(ClaudeLocator.Find(profile, roaming, local) == store, "Store precedence");
    File.Delete(store);
    File.Delete(desktop);
    File.Delete(native);
    string data = Path.Combine(local, "Claude-Data", "claude_2.1", "claude-code", "bin", "2.1", "claude.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(data)!);
    File.WriteAllText(data, "");
    Check(ClaudeLocator.Find(profile, roaming, local) == data, "Claude-Data layout");
}
finally
{
    if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
}
Console.WriteLine($"{passed} checks passed.");
