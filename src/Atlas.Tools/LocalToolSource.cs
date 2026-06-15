namespace Atlas.Tools;

/// <summary>
/// A <see cref="IToolSource"/> over the built-in tools registered in the
/// container. Always available and synchronous to list.
/// </summary>
public sealed class LocalToolSource : IToolSource
{
    private readonly IReadOnlyList<ITool> _tools;

    /// <summary>Creates the source from the registered built-in tools.</summary>
    public LocalToolSource(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = [.. tools];
    }

    /// <inheritdoc />
    public string Name => "local";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ITool>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_tools);
}
