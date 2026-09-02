using SectorFlow.LmuNetworkAnalyzer.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SectorFlow.LmuNetworkAnalyzer.Services;

public sealed class NetworkDiagnostics
{
    public async Task<NetworkSample> PingOnceAsync(string target, int timeoutMs = 1500)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, timeoutMs);

            return reply.Status == IPStatus.Success
                ? new NetworkSample(DateTime.Now, target, true, reply.RoundtripTime, reply.Address?.ToString() ?? "OK")
                : new NetworkSample(DateTime.Now, target, false, null, reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new NetworkSample(DateTime.Now, target, false, null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> TraceRouteAsync(string target, int maxHops = 30, int timeoutMs = 1500)
    {
        var results = new List<string>();
        byte[] buffer = new byte[32];

        for (int ttl = 1; ttl <= maxHops; ttl++)
        {
            try
            {
                using var ping = new Ping();
                var options = new PingOptions(ttl, false);
                var reply = await ping.SendPingAsync(target, timeoutMs, buffer, options);

                var address = reply.Address?.ToString() ?? "*";
                var time = reply.Status == IPStatus.TimedOut ? "*" : $"{reply.RoundtripTime} ms";
                results.Add($"{ttl,2}  {address,-42} {time}");

                if (reply.Status == IPStatus.Success)
                    break;
            }
            catch (Exception ex)
            {
                results.Add($"{ttl,2}  ERROR: {ex.Message}");
                break;
            }
        }

        return results;
    }

    public async Task<int?> DiscoverIpv4MtuAsync(string target, int timeoutMs = 1500)
    {
        if (!IPAddress.TryParse(target, out var ip))
        {
            try
            {
                ip = (await Dns.GetHostAddressesAsync(target))
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            catch
            {
                return null;
            }
        }

        if (ip is null || ip.AddressFamily != AddressFamily.InterNetwork)
            return null;

        int low = 1200;
        int high = 1472;
        int bestPayload = -1;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(
                    ip,
                    timeoutMs,
                    new byte[mid],
                    new PingOptions(64, true));

                if (reply.Status == IPStatus.Success)
                {
                    bestPayload = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            catch
            {
                high = mid - 1;
            }
        }

        return bestPayload < 0 ? null : bestPayload + 28;
    }
}
