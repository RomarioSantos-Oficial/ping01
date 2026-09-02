using System.Diagnostics;

namespace SectorFlow.LmuNetworkAnalyzer.Services;

public sealed class OptimizerService
{
    public string ApplySafeLocalOptimizations(Process? lmu)
    {
        var notes = new List<string>();

        if (lmu is null || lmu.HasExited)
        {
            notes.Add("LMU process not found; process priority was not changed.");
        }
        else
        {
            try
            {
                lmu.PriorityClass = ProcessPriorityClass.AboveNormal;
                notes.Add("LMU process priority set to AboveNormal.");
            }
            catch (Exception ex)
            {
                notes.Add($"Could not change LMU priority: {ex.Message}");
            }
        }

        notes.Add("No VPN, packet injection, firewall bypass, route hijack or anti-cheat modification was applied.");
        notes.Add("Internet BGP route is controlled by the ISP; this tool only applies local safe optimizations.");

        return string.Join(Environment.NewLine, notes);
    }

    public void LaunchQosInstaller()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "scripts", "Apply-LmuQoS.ps1");
        if (!File.Exists(script))
            throw new FileNotFoundException("QoS script not found.", script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(psi);
    }
}
