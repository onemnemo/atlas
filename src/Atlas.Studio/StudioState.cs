using Atlas.Core.Permissions;
using Atlas.Core.Results;

namespace Atlas.Studio;

/// <summary>
/// Mutable UI session state shared across screens — the user's current
/// selections and the latest pipeline result.
/// </summary>
/// <remarks>
/// This is presentation state only. It never holds orchestration logic; it is the
/// small bag of "what the operator currently has selected" (permissions to apply,
/// the last run to inspect, the observed backend health) that several screens
/// read and write.
/// </remarks>
internal sealed class StudioState
{
    /// <summary>The permission level applied to requests sent from the dashboard.</summary>
    public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.Read;

    /// <summary>Whether gated external (internet) access is granted for the session.</summary>
    public bool GrantExternal { get; set; }

    /// <summary>Whether private-memory access is granted for the session.</summary>
    public bool GrantPrivateMemory { get; set; }

    /// <summary>The most recent backend-health reading (updated by a background poll).</summary>
    public volatile bool BackendHealthy;

    /// <summary>The inference base URL the dashboard is pointed at (for display).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Seconds between automatic backend-health checks.</summary>
    public int HealthPollSeconds { get; set; } = 3;

    /// <summary>Round-trip time of the last health check, in milliseconds.</summary>
    public double LastHealthLatencyMs { get; set; }

    /// <summary>When the last health check completed.</summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>Set by the host so screens can trigger an immediate health re-check.</summary>
    public Action? RequestHealthRecheck { get; set; }

    /// <summary>The most recent pipeline result, for the run inspector.</summary>
    public PipelineResult? LastResult { get; set; }

    /// <summary>Session activity counters.</summary>
    public SessionMetrics Metrics { get; } = new();

    /// <summary>Builds the permission state to attach to outgoing requests.</summary>
    public PermissionState BuildPermissions()
    {
        ResourceGate gates = ResourceGate.None;
        if (GrantExternal)
        {
            gates |= ResourceGate.GatedExternal;
        }

        if (GrantPrivateMemory)
        {
            gates |= ResourceGate.PrivateMemory;
        }

        return new PermissionState(PermissionLevel, gates);
    }
}

/// <summary>Running counts and latency for requests issued this session.</summary>
internal sealed class SessionMetrics
{
    private double _totalLatencyMs;

    public int Total { get; private set; }
    public int Successes { get; private set; }
    public int Degraded { get; private set; }
    public int Escalated { get; private set; }
    public int Failures { get; private set; }
    public double LastLatencyMs { get; private set; }
    public double AverageLatencyMs => Total > 0 ? _totalLatencyMs / Total : 0;

    public void Record(OutcomeStatus status, double latencyMs)
    {
        Total++;
        LastLatencyMs = latencyMs;
        _totalLatencyMs += latencyMs;

        switch (status)
        {
            case OutcomeStatus.Success:
                Successes++;
                break;
            case OutcomeStatus.Degraded:
                Degraded++;
                break;
            case OutcomeStatus.Escalated:
                Escalated++;
                break;
            default:
                Failures++;
                break;
        }
    }
}
