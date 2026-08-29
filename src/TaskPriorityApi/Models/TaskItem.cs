namespace TaskPriorityApi.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DeadlineDays { get; set; }
    public string? Assignee { get; set; }
    public string? Tags { get; set; }
    public float? Score { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
