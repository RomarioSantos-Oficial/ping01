using SectorFlow.LmuNetworkAnalyzer.Models;
using System.Globalization;
using System.Text;

namespace SectorFlow.LmuNetworkAnalyzer.Services;

public sealed class ReportService
{
    public string Export(
        IEnumerable<NetworkSample> samples,
        string target,
        string? lmuLogFile,
        IEnumerable<string> disconnectLines,
        string summary)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SectorFlowLMU",
            "Reports");

        Directory.CreateDirectory(directory);

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var basePath = Path.Combine(directory, $"LMU_Network_Report_{stamp}");

        var csvPath = basePath + ".csv";
        var txtPath = basePath + ".txt";

        var csv = new StringBuilder();
        csv.AppendLine("timestamp,target,success,latency_ms,note");

        foreach (var s in samples)
        {
            csv.Append(EscapeCsv(s.Timestamp.ToString("O"))).Append(',')
               .Append(EscapeCsv(s.Target)).Append(',')
               .Append(s.Success ? "1" : "0").Append(',')
               .Append(s.LatencyMs?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
               .Append(EscapeCsv(s.Note)).AppendLine();
        }

        File.WriteAllText(csvPath, csv.ToString(), Encoding.UTF8);

        var txt = new StringBuilder();
        txt.AppendLine("SECTORFLOW LMU NETWORK REPORT");
        txt.AppendLine(new string('=', 42));
        txt.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        txt.AppendLine($"Target: {target}");
        txt.AppendLine($"LMU log: {lmuLogFile ?? "not found"}");
        txt.AppendLine();
        txt.AppendLine("SUMMARY");
        txt.AppendLine(summary);
        txt.AppendLine();
        txt.AppendLine("RECENT LMU DISCONNECT/TIMEOUT LINES");

        foreach (var line in disconnectLines)
            txt.AppendLine(line);

        File.WriteAllText(txtPath, txt.ToString(), Encoding.UTF8);
        return txtPath;
    }

    private static string EscapeCsv(string value)
        => """ + value.Replace(""", """") + """;
}
