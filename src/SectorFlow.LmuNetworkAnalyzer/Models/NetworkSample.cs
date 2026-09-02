namespace SectorFlow.LmuNetworkAnalyzer.Models;

public sealed record NetworkSample(
    DateTime Timestamp,
    string Target,
    bool Success,
    long? LatencyMs,
    string Note);
