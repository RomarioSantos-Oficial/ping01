using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace SectorFlow.LmuNetworkAnalyzer.Services;

public sealed class LmuLocator
{
    private static readonly Regex IpRegex = new(
        @"(?<!\d)(?<ip>(?:\d{1,3}\.){3}\d{1,3})(?::(?<port>\d{2,5}))?",
        RegexOptions.Compiled);

    public Process? FindProcess()
    {
        return Process.GetProcesses()
            .FirstOrDefault(p =>
            {
                try
                {
                    return p.ProcessName.Contains("Le Mans Ultimate", StringComparison.OrdinalIgnoreCase)
                        || p.ProcessName.Contains("LeMansUltimate", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });
    }

    public string? FindLogDirectory()
    {
        foreach (var candidate in GetCandidateLogDirectories())
        {
            try
            {
                if (Directory.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignore inaccessible drives/paths.
            }
        }

        return null;
    }

    public IReadOnlyList<string> DiscoverPublicServerIps(int maxFiles = 5)
    {
        var logDir = FindLogDirectory();
        if (logDir is null)
            return Array.Empty<string>();

        var files = Directory.EnumerateFiles(logDir, "*.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(maxFiles)
            .ToArray();

        var results = new List<string>();

        foreach (var file in files)
        {
            string text;
            try
            {
                text = ReadTail(file, 2 * 1024 * 1024);
            }
            catch
            {
                continue;
            }

            foreach (Match match in IpRegex.Matches(text))
            {
                var ipText = match.Groups["ip"].Value;
                if (!IPAddress.TryParse(ipText, out var ip))
                    continue;
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    continue;
                if (!IsPublicIpv4(ip))
                    continue;

                if (!results.Contains(ipText, StringComparer.OrdinalIgnoreCase))
                    results.Add(ipText);
            }
        }

        return results.Take(25).ToArray();
    }

    public string? GetNewestLogFile()
    {
        var dir = FindLogDirectory();
        if (dir is null)
            return null;

        return Directory.EnumerateFiles(dir, "*.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public IReadOnlyList<string> ReadRecentDisconnectLines(int maxLines = 30)
    {
        var file = GetNewestLogFile();
        if (file is null)
            return Array.Empty<string>();

        try
        {
            var tail = ReadTail(file, 1024 * 1024);
            return tail.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line =>
                    line.Contains("disconnect", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("connection lost", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("lost connection", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                .TakeLast(maxLines)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> GetCandidateLogDirectories()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath))
                roots.Add(steamPath.Replace('/', Path.DirectorySeparatorChar));
        }
        catch { }

        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            roots.Add(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary"));
            roots.Add(Path.Combine(drive.RootDirectory.FullName, "Steam"));
        }

        foreach (var root in roots)
        {
            yield return Path.Combine(root, "steamapps", "common", "Le Mans Ultimate", "UserData", "Log");
        }
    }

    private static bool IsPublicIpv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();

        if (b[0] == 10 || b[0] == 127 || b[0] == 0)
            return false;
        if (b[0] == 169 && b[1] == 254)
            return false;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return false;
        if (b[0] == 192 && b[1] == 168)
            return false;
        if (b[0] >= 224)
            return false;

        return true;
    }

    private static string ReadTail(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var start = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
