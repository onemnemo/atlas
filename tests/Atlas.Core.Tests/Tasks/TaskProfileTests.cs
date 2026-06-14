using Atlas.Core.Tasks;
using Xunit;

namespace Atlas.Core.Tests.Tasks;

public sealed class TaskProfileTests
{
    [Fact]
    public void All_default_profiles_are_valid()
    {
        foreach (TaskProfile profile in DefaultTaskProfiles.All)
        {
            // Should not throw.
            profile.Validate();
        }
    }

    [Fact]
    public void Default_registry_resolves_every_well_known_task_id()
    {
        string[] knownIds =
        [
            TaskIds.Autocomplete,
            TaskIds.InlineRewrite,
            TaskIds.ChatResponse,
            TaskIds.FlashcardGeneration,
            TaskIds.LearningPathGeneration,
            TaskIds.MindmapEditing,
            TaskIds.FileIngestion,
        ];

        foreach (string id in knownIds)
        {
            Assert.True(TaskProfileRegistry.Default.TryGet(id, out TaskProfile? profile), $"missing profile: {id}");
            Assert.NotNull(profile);
            Assert.Equal(id, profile!.TaskId);
        }
    }

    [Fact]
    public void Registry_lookup_is_case_insensitive()
    {
        Assert.True(TaskProfileRegistry.Default.TryGet(TaskIds.ChatResponse.ToUpperInvariant(), out TaskProfile? profile));
        Assert.NotNull(profile);
    }

    [Fact]
    public void Unknown_task_id_does_not_resolve()
    {
        Assert.False(TaskProfileRegistry.Default.TryGet("does.not.exist", out TaskProfile? profile));
        Assert.Null(profile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_rejects_non_positive_context_budget(int budget)
    {
        TaskProfile profile = DefaultTaskProfiles.ChatResponse with { ContextBudgetTokens = budget };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void Validate_rejects_negative_retry_count()
    {
        TaskProfile profile = DefaultTaskProfiles.ChatResponse with { MaxRetries = -1 };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void Duplicate_ids_are_rejected_by_the_registry()
    {
        Assert.Throws<ArgumentException>(() =>
            new TaskProfileRegistry([DefaultTaskProfiles.ChatResponse, DefaultTaskProfiles.ChatResponse]));
    }

    [Fact]
    public void Learning_path_is_the_most_demanding_profile()
    {
        // Sanity-check the benchmark task (arch §22) carries the strictest settings.
        TaskProfile lp = DefaultTaskProfiles.LearningPathGeneration;

        Assert.Equal(RetrievalDepth.Deep, lp.RetrievalDepth);
        Assert.Equal(ValidationStrictness.Paranoid, lp.ValidationStrictness);
        Assert.True(lp.Resumable);
        Assert.True(lp.CitationRequired);
        Assert.Equal(3, lp.MaxRetries);
    }
}
