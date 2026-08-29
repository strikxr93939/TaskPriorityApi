using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using TaskPriorityApi.Models;
using TaskPriorityApi.Services;

namespace TaskPriorityApi.Tests;

public class TaskRankerTests
{
    private static TaskRanker CreateRanker() => new(NullLogger<TaskRanker>.Instance);

    private static TaskItem Make(int id, string title, int deadline, string? assignee, string? tags) =>
        new() { Id = id, Title = title, DeadlineDays = deadline, Assignee = assignee, Tags = tags };

    [Fact]
    public async Task UrgentTaskScoresHigherThanFarDeadline()
    {
        var ranker = CreateRanker();
        await ranker.EnsureModelAsync();

        var urgent = Make(1, "Fix prod outage", 1, "alice", "urgent;bug");
        var far = Make(2, "Update docs", 45, "bob", "docs");

        var ranked = ranker.Rank(new[] { urgent, far });

        Assert.Equal(urgent.Id, ranked[0].Id);
        Assert.True(ranked[0].Score > ranked[1].Score,
            $"Ожидался более высокий score для срочной задачи: urgent={ranked[0].Score}, far={ranked[1].Score}");
    }

    [Fact]
    public async Task ScoresAreWithinZeroToHundredAndReasonsAreNotEmpty()
    {
        var ranker = CreateRanker();
        await ranker.EnsureModelAsync();

        var tasks = new[]
        {
            Make(1, "Hotfix critical bug", 0, null, "urgent;bug"),
            Make(2, "Refactor module", 10, "carol", "refactor"),
            Make(3, "Cleanup repo", 60, "dave", "docs;test;infra;refactor")
        };

        var ranked = ranker.Rank(tasks);

        Assert.All(ranked, r =>
        {
            Assert.InRange(r.Score, 0f, 100f);
            Assert.False(string.IsNullOrWhiteSpace(r.Reason));
        });
        Assert.Equal(3, ranked.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task RankingIsDeterministic()
    {
        var ranker = CreateRanker();
        await ranker.EnsureModelAsync();

        var tasks = new[]
        {
            Make(1, "Task A", 2, null, "urgent"),
            Make(2, "Task B", 15, "bob", "feature"),
            Make(3, "Task C", 30, "carol", "docs")
        };

        var first = ranker.Rank(tasks);
        var second = ranker.Rank(tasks);

        Assert.Equal(first.Select(r => (r.Id, r.Score)), second.Select(r => (r.Id, r.Score)));
    }

    [Fact]
    public async Task ReasonMentionsMissingAssignee()
    {
        var ranker = CreateRanker();
        await ranker.EnsureModelAsync();

        var task = Make(1, "Some task", 5, null, "feature");
        var ranked = ranker.Rank(task);

        Assert.Contains("нет исполнителя", ranked.Reason);
    }

    [Fact]
    public async Task ReasonMentionsCriticalDeadlineForUrgentTask()
    {
        var ranker = CreateRanker();
        await ranker.EnsureModelAsync();

        var task = Make(1, "Hotfix", 1, "alice", "bug");
        var ranked = ranker.Rank(task);

        Assert.Contains("критичный дедлайн", ranked.Reason);
        Assert.True(ranked.Score >= 70, $"Срочная задача должна иметь высокий score, получено {ranked.Score}");
    }
}
