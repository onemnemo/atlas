using Atlas.Core.Budgeting;
using Xunit;

namespace Atlas.Core.Tests.Budgeting;

public sealed class ContextBudgetTests
{
    [Fact]
    public void Default_allocation_is_valid_and_sums_to_one()
    {
        // Should not throw.
        BudgetAllocation.Default.Validate();
    }

    [Fact]
    public void Slots_never_exceed_the_total_budget()
    {
        ContextBudget budget = ContextBudget.Create(4096);

        int sum = budget.SystemOverheadTokens
                  + budget.TaskInstructionTokens
                  + budget.RetrievedContentTokens
                  + budget.GenerationTokens;

        // Flooring each slot guarantees we never over-allocate the budget.
        Assert.True(sum <= budget.TotalTokens, $"slots summed to {sum}, exceeding {budget.TotalTokens}");
    }

    [Fact]
    public void Retrieved_content_gets_the_largest_share_by_default()
    {
        ContextBudget budget = ContextBudget.Create(10_000);

        Assert.True(budget.RetrievedContentTokens >= budget.GenerationTokens);
        Assert.True(budget.RetrievedContentTokens >= budget.TaskInstructionTokens);
        Assert.True(budget.RetrievedContentTokens >= budget.SystemOverheadTokens);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_total(int total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContextBudget.Create(total));
    }

    [Fact]
    public void Allocation_that_does_not_sum_to_one_is_rejected()
    {
        var bad = new BudgetAllocation(0.5, 0.5, 0.5, 0.5);
        Assert.Throws<ArgumentException>(bad.Validate);
    }
}
