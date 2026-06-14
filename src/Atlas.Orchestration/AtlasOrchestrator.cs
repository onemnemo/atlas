using Atlas.Core;
using Atlas.Core.Budgeting;
using Atlas.Core.Diagnostics;
using Atlas.Core.Hardware;
using Atlas.Core.Results;
using Atlas.Core.Tasks;
using Atlas.Orchestration.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Orchestration;

/// <summary>
/// The default <see cref="IAtlasOrchestrator"/>: the controller that turns a
/// request into a routed, budgeted, honestly-reported pipeline run (arch §28).
/// </summary>
/// <remarks>
/// <para>
/// The orchestrator's responsibility is deliberately narrow (arch §28): resolve
/// the task profile, pick the route, assemble the scoped budget, and run it. It
/// does not touch tools, documents, or the model directly — the route and its
/// stages do. Keeping the controller thin is what makes the system predictable.
/// </para>
/// <para>
/// It is also the outermost degradation boundary. No matter what a route does,
/// the orchestrator returns an honest <see cref="PipelineResult"/> — never an
/// unhandled exception — including for unknown tasks, missing routes,
/// cancellation, and unexpected errors (arch §26).
/// </para>
/// </remarks>
public sealed partial class AtlasOrchestrator : IAtlasOrchestrator
{
    private readonly ITaskProfileProvider _profiles;
    private readonly IRequestRouter _router;
    private readonly Dictionary<string, IPipelineRoute> _routes;
    private readonly HardwareProfile _hardware;
    private readonly ILogger<AtlasOrchestrator> _logger;

    /// <summary>Creates the orchestrator.</summary>
    /// <param name="profiles">Resolves task profiles by id.</param>
    /// <param name="router">Selects the route for a request.</param>
    /// <param name="routes">The available pipeline routes.</param>
    /// <param name="hardware">The detected hardware profile for this run host.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentException">Two routes declare the same task id.</exception>
    public AtlasOrchestrator(
        ITaskProfileProvider profiles,
        IRequestRouter router,
        IEnumerable<IPipelineRoute> routes,
        HardwareProfile hardware,
        ILogger<AtlasOrchestrator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(hardware);

        _profiles = profiles;
        _router = router;
        _hardware = hardware;
        _logger = logger ?? NullLogger<AtlasOrchestrator>.Instance;

        var map = new Dictionary<string, IPipelineRoute>(StringComparer.OrdinalIgnoreCase);
        foreach (IPipelineRoute route in routes)
        {
            if (!map.TryAdd(route.TaskId, route))
            {
                throw new ArgumentException($"Duplicate pipeline route for task id '{route.TaskId}'.", nameof(routes));
            }
        }

        _routes = map;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> ExecuteAsync(
        PipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string routeKey = _router.Route(request);

        if (!_profiles.TryGet(routeKey, out TaskProfile? profile))
        {
            LogUnknownTask(_logger, routeKey);
            return PipelineResult.Failed(request.RequestId,
            [
                AtlasWarning.Error(FailureMode.None, $"No task profile is registered for '{routeKey}'."),
            ]);
        }

        if (!_routes.TryGetValue(routeKey, out IPipelineRoute? route))
        {
            LogNoRoute(_logger, routeKey);
            return PipelineResult.Failed(request.RequestId,
            [
                AtlasWarning.Error(FailureMode.None, $"No pipeline route can handle '{routeKey}'."),
            ]);
        }

        int effectiveBudget = HardwareBudgetPolicy.Scale(profile.ContextBudgetTokens, _hardware.Tier);
        var runContext = new PipelineRunContext(
            request, profile, _hardware, ContextBudget.Create(effectiveBudget), _logger);

        LogRunStarted(_logger, routeKey, effectiveBudget, request.RequestId);

        try
        {
            PipelineResult result = await route.ExecuteAsync(runContext, cancellationToken).ConfigureAwait(false);
            LogRunCompleted(_logger, routeKey, result.Status);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PipelineResult.Escalated(request.RequestId,
            [
                AtlasWarning.Error(FailureMode.ModelCallTimeout, "The request was cancelled before it completed."),
            ]);
        }
#pragma warning disable CA1031 // The orchestrator is the outermost degradation boundary; it must never leak.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnexpectedError(_logger, routeKey, ex);
            return PipelineResult.Failed(request.RequestId,
            [
                AtlasWarning.Error(FailureMode.None, "An unexpected error occurred while handling the request.", ex.Message),
            ]);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Running task '{RouteKey}' (budget {BudgetTokens} tokens, request {RequestId}).")]
    private static partial void LogRunStarted(ILogger logger, string routeKey, int budgetTokens, Guid requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task '{RouteKey}' completed with status {Status}.")]
    private static partial void LogRunCompleted(ILogger logger, string routeKey, Atlas.Core.Results.OutcomeStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No task profile registered for route key '{RouteKey}'.")]
    private static partial void LogUnknownTask(ILogger logger, string routeKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No pipeline route handles task '{RouteKey}'.")]
    private static partial void LogNoRoute(ILogger logger, string routeKey);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error while running route '{RouteKey}'.")]
    private static partial void LogUnexpectedError(ILogger logger, string routeKey, Exception exception);
}
