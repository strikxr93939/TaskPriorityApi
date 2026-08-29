using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskPriorityApi.Data;
using TaskPriorityApi.DTOs;
using TaskPriorityApi.ML;
using TaskPriorityApi.Models;
using TaskPriorityApi.Services;
using TaskPriorityApi.Utils;

namespace TaskPriorityApi.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(TaskContext db, ITaskRanker ranker, ILogger<TasksController> logger) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResultDto>> UploadCsv(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Файл CSV обязателен" });

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync(ct);

        var rows = CsvParser.Parse(content);
        if (rows.Count == 0)
            return BadRequest(new { error = "CSV пуст" });

        var header = rows[0].Select(h => h.ToLowerInvariant()).ToArray();
        var index = new Dictionary<string, int>();
        for (int i = 0; i < header.Length; i++) index.TryAdd(header[i], i);

        foreach (var column in new[] { "title", "deadline_days", "assignee", "tags" })
            if (!index.ContainsKey(column))
                return BadRequest(new { error = $"В CSV отсутствует колонка '{column}'" });

        var errors = new List<string>();
        var created = 0;

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Get(string name) => index[name] < row.Length ? row[index[name]] : "";

            var title = Get("title");
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add($"Строка {r + 1}: пустой title");
                continue;
            }
            if (!int.TryParse(Get("deadline_days"), out var deadline) || deadline < 0)
            {
                errors.Add($"Строка {r + 1}: некорректный deadline_days");
                continue;
            }

            db.Tasks.Add(new TaskItem
            {
                Title = title.Trim(),
                DeadlineDays = deadline,
                Assignee = Get("assignee"),
                Tags = Get("tags")
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        if (created == 0)
            return BadRequest(new { error = "Не удалось импортировать ни одну задачу", errors });

        logger.LogInformation("Загружено {Created} задач из CSV", created);
        return Ok(new UploadResultDto(created, rows.Count - 1 - created, errors));
    }

    [HttpPost("upload")]
    [Consumes("application/json")]
    public async Task<ActionResult<UploadResultDto>> UploadJson([FromBody] List<UploadTaskDto>? tasks, CancellationToken ct)
    {
        if (tasks is null || tasks.Count == 0)
            return BadRequest(new { error = "Список задач пуст" });

        var errors = new List<string>();
        var created = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            var dto = tasks[i];
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                errors.Add($"Задача {i + 1}: пустой title");
                continue;
            }
            if (dto.DeadlineDays < 0)
            {
                errors.Add($"Задача {i + 1}: некорректный deadline_days");
                continue;
            }

            db.Tasks.Add(new TaskItem
            {
                Title = dto.Title.Trim(),
                DeadlineDays = dto.DeadlineDays,
                Assignee = dto.Assignee,
                Tags = dto.Tags
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        if (created == 0)
            return BadRequest(new { error = "Не удалось импортировать ни одну задачу", errors });

        return Ok(new UploadResultDto(created, tasks.Count - created, errors));
    }

    [HttpPost("rank")]
    public async Task<ActionResult<List<RankedTaskDto>>> Rank(CancellationToken ct)
    {
        await ranker.EnsureModelAsync(ct);

        var tasks = await db.Tasks.ToListAsync(ct);
        if (tasks.Count == 0)
            return Ok(new List<RankedTaskDto>());

        var ranked = ranker.Rank(tasks);

        var byId = tasks.ToDictionary(t => t.Id);
        foreach (var item in ranked)
            byId[item.Id].Score = item.Score;

        await db.SaveChangesAsync(ct);
        return Ok(ranked);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDetailsDto>> Get(int id, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound(new { error = $"Задача {id} не найдена" });

        return Ok(new TaskDetailsDto(
            task.Id,
            task.Title,
            task.DeadlineDays,
            task.Assignee,
            [.. PriorityData.SplitTags(task.Tags)],
            task.Score,
            task.CreatedAt));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<List<TagStatDto>>> Stats(CancellationToken ct)
    {
        var tasks = await db.Tasks
            .AsNoTracking()
            .Where(t => t.Score != null)
            .Select(t => new { t.Tags, t.Score })
            .ToListAsync(ct);

        var stats = tasks
            .SelectMany(t => PriorityData.SplitTags(t.Tags).Select(tag => new { Tag = tag, Score = t.Score!.Value }))
            .GroupBy(x => x.Tag)
            .Select(g => new TagStatDto(g.Key, Math.Round(g.Average(x => x.Score), 1), g.Count()))
            .OrderByDescending(s => s.AverageScore)
            .ToList();

        return Ok(stats);
    }
}
