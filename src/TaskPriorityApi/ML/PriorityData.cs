using TaskPriorityApi.Models;

namespace TaskPriorityApi.ML;

public class PriorityData
{
    public float DeadlineDays { get; set; }
    public float AssigneeKnown { get; set; }
    public float TagCount { get; set; }
    public float HasUrgentTag { get; set; }
    public float TitleLength { get; set; }
    public float Score { get; set; }

    public static PriorityData From(TaskItem task) => new()
    {
        DeadlineDays = task.DeadlineDays,
        AssigneeKnown = string.IsNullOrWhiteSpace(task.Assignee) ? 0f : 1f,
        TagCount = SplitTags(task.Tags).Length,
        HasUrgentTag = SplitTags(task.Tags).Any(t => t.Equals("urgent", StringComparison.OrdinalIgnoreCase)) ? 1f : 0f,
        TitleLength = task.Title?.Length ?? 0
    };

    public static string[] SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class ScorePrediction
{
    [Microsoft.ML.Data.ColumnName("Score")]
    public float Score { get; set; }
}
