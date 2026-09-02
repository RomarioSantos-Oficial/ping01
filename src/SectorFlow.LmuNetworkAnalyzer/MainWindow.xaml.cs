using SectorFlow.LmuNetworkAnalyzer.Models;
using SectorFlow.LmuNetworkAnalyzer.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace SectorFlow.LmuNetworkAnalyzer;

public partial class MainWindow : Window
{
    private readonly LmuLocator _lmuLocator = new();
    private readonly NetworkDiagnostics _diagnostics = new();
    private readonly ReportService _reportService = new();
    private readonly OptimizerService _optimizer = new();
    private readonly ObservableCollection<NetworkSample> _samples = new();

    private CancellationTokenSource? _monitorCts;
    private Process? _lmuProcess;

    public MainWindow()
    {
        InitializeComponent();
        SamplesGrid.ItemsSource = _samples;
        DetectLmu();
    }

    private void DetectLmu_Click(object sender, RoutedEventArgs e) => DetectLmu();

    private void DetectLmu()
    {
        _lmuProcess = _lmuLocator.FindProcess();

        if (_lmuProcess is null)
        {
            ProcessStatusText.Text = "Not detected";
            StatusText.Text = "LMU is not running. You can still test a manual server IP.";
            return;
        }

        ProcessStatusText.Text = $"Running (PID {_lmuProcess.Id})";
        StatusText.Text = "LMU detected.";
    }

    private void DiscoverServer_Click(object sender, RoutedEventArgs e)
    {
        var ips = _lmuLocator.DiscoverPublicServerIps();

        if (ips.Count == 0)
        {
            OutputTextBox.Text =
                "No public server IP was found in recent LMU logs.\n" +
                "This is possible with UDP/RUDP sessions. Use Resource Monitor or enter the Race Server IP manually.";
            return;
        }

        TargetTextBox.Text = ips[0];
        OutputTextBox.Text =
            "Public IP candidates found in recent LMU logs:\n\n" +
            string.Join("\n", ips) +
            "\n\nThe first IP was selected. Confirm it while entering a Race session.";
    }

    private async void Monitor_Click(object sender, RoutedEventArgs e)
    {
        if (_monitorCts is not null)
        {
            _monitorCts.Cancel();
            _monitorCts = null;
            MonitorButton.Content = "Start monitor";
            StatusText.Text = "Monitor stopped.";
            return;
        }

        var target = TargetTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show("Enter a server IP or hostname.", "SectorFlow LMU");
            return;
        }

        _monitorCts = new CancellationTokenSource();
        MonitorButton.Content = "Stop monitor";
        StatusText.Text = $"Monitoring {target}...";

        try
        {
            while (!_monitorCts.IsCancellationRequested)
            {
                var sample = await _diagnostics.PingOnceAsync(target);
                _samples.Add(sample);

                while (_samples.Count > 600)
                    _samples.RemoveAt(0);

                UpdateMetrics();

                await Task.Delay(1000, _monitorCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_monitorCts is not null && _monitorCts.IsCancellationRequested)
            {
                _monitorCts.Dispose();
                _monitorCts = null;
            }

            MonitorButton.Content = "Start monitor";
        }
    }

    private async void Traceroute_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return;

        StatusText.Text = "Running traceroute...";
        OutputTextBox.Text = "Traceroute can show * on routers that block ICMP.\n\n";

        var route = await _diagnostics.TraceRouteAsync(target);
        OutputTextBox.Text += string.Join(Environment.NewLine, route);
        StatusText.Text = "Traceroute finished.";
    }

    private async void Mtu_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return;

        StatusText.Text = "Testing IPv4 MTU...";
        var mtu = await _diagnostics.DiscoverIpv4MtuAsync(target);

        OutputTextBox.Text = mtu is null
            ? "MTU test could not complete. The target may block ICMP/Don't Fragment probes."
            : $"Largest tested path MTU without fragmentation: approximately {mtu} bytes.\n\n" +
              "Do not change the Windows adapter MTU only from this result; compare multiple stable targets first.";

        StatusText.Text = "MTU test finished.";
    }

    private void Optimize_Click(object sender, RoutedEventArgs e)
    {
        DetectLmu();
        var result = _optimizer.ApplySafeLocalOptimizations(_lmuProcess);
        OutputTextBox.Text = result;
        StatusText.Text = "Safe local optimization applied where possible.";
    }

    private void Qos_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _optimizer.LaunchQosInstaller();
            StatusText.Text = "Windows requested administrator permission to install the LMU QoS policy.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "QoS error");
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var target = TargetTextBox.Text.Trim();
        var summary = BuildSummary();
        var path = _reportService.Export(
            _samples,
            target,
            _lmuLocator.GetNewestLogFile(),
            _lmuLocator.ReadRecentDisconnectLines(),
            summary);

        OutputTextBox.Text = $"Report exported to:\n{path}";
        StatusText.Text = "Report exported.";
    }

    private void UpdateMetrics()
    {
        var target = TargetTextBox.Text.Trim();
        var relevant = _samples
            .Where(s => string.Equals(s.Target, target, StringComparison.OrdinalIgnoreCase))
            .TakeLast(120)
            .ToArray();

        if (relevant.Length == 0)
            return;

        var successful = relevant.Where(s => s.Success && s.LatencyMs.HasValue).ToArray();
        var latest = successful.LastOrDefault();
        PingText.Text = latest?.LatencyMs is long ms ? $"{ms} ms" : "-";

        var latencies = successful.Select(s => (double)s.LatencyMs!.Value).ToArray();
        var jitter = CalculateJitter(latencies);
        JitterText.Text = latencies.Length >= 2 ? $"{jitter:F1} ms" : "-";

        var loss = 100.0 * relevant.Count(s => !s.Success) / relevant.Length;
        LossText.Text = $"{loss:F1}%";
    }

    private string BuildSummary()
    {
        var target = TargetTextBox.Text.Trim();
        var relevant = _samples
            .Where(s => string.Equals(s.Target, target, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (relevant.Length == 0)
            return "No samples recorded.";

        var success = relevant.Where(s => s.Success && s.LatencyMs.HasValue).ToArray();
        var loss = 100.0 * relevant.Count(s => !s.Success) / relevant.Length;

        if (success.Length == 0)
            return $"Samples: {relevant.Length}\nLoss/unanswered ICMP: {loss:F1}%\nNo successful ICMP replies.";

        var values = success.Select(s => (double)s.LatencyMs!.Value).ToArray();
        var avg = values.Average();
        var jitter = CalculateJitter(values);
        var max = values.Max();
        var min = values.Min();

        return
            $"Samples: {relevant.Length}\n" +
            $"Successful ICMP: {success.Length}\n" +
            $"Average ping: {avg:F1} ms\n" +
            $"Min: {min:F0} ms\n" +
            $"Max: {max:F0} ms\n" +
            $"Jitter (mean absolute delta): {jitter:F1} ms\n" +
            $"Loss/unanswered ICMP: {loss:F1}%\n" +
            "Note: some game servers/routers block ICMP, so ICMP loss alone does not prove game packet loss.";
    }

    private static double CalculateJitter(double[] latencies)
    {
        if (latencies.Length < 2)
            return 0;

        double total = 0;
        for (int i = 1; i < latencies.Length; i++)
            total += Math.Abs(latencies[i] - latencies[i - 1]);

        return total / (latencies.Length - 1);
    }
}
