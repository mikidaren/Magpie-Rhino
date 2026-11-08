namespace RhinoMagpie;

public static class ClaudeLocator
{
    public static string? Find(string profile, string roaming, string local)
    {
        foreach (string package in Directories(Path.Combine(local, "Packages"), "*laude*"))
        {
            string? candidate = ExecutableUnder(Path.Combine(package, "LocalCache", "Roaming", "Claude", "claude-code"));
            if (candidate is not null) return candidate;
        }
        string? desktop = ExecutableUnder(Path.Combine(roaming, "Claude", "claude-code"));
        if (desktop is not null) return desktop;
        foreach (string candidate in new[]
        {
            Path.Combine(profile, ".local", "bin", "claude.exe"),
            Path.Combine(roaming, "npm", "claude.cmd"),
        })
            if (File.Exists(candidate)) return candidate;
        foreach (string installation in Directories(Path.Combine(local, "Claude-Data"), "claude_*"))
        {
            string? candidate = ExecutableUnder(Path.Combine(installation, "claude-code"));
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> Directories(string root, string pattern) =>
        Directory.Exists(root) ? Directory.EnumerateDirectories(root, pattern).Order() : [];

    private static string? ExecutableUnder(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "claude.exe", SearchOption.AllDirectories).Order().FirstOrDefault()
            : null;
}
