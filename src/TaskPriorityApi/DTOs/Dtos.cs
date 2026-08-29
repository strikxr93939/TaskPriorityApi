namespace TaskPriorityApi.DTOs;

public record RankedTaskDto(int Id, string Title, float Score, string Reason);

public record TaskDetailsDto(int Id, string Title, int DeadlineDays, string? Assignee, List<string> Tags, float? Score, DateTime CreatedAt);

public record TagStatDto(string Tag, double AverageScore, int Count);

public record UploadTaskDto(string Title, int DeadlineDays, string? Assignee, string? Tags);

public record UploadResultDto(int Created, int Skipped, List<string> Errors);
