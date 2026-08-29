using Microsoft.ML;
using TaskPriorityApi.DTOs;
using TaskPriorityApi.ML;
using TaskPriorityApi.Models;

namespace TaskPriorityApi.Services;

public interface ITaskRanker
{
    Task EnsureModelAsync(CancellationToken ct = default);
    bool WasJustTrained { get; }
    double LastRSquared { get; }
    RankedTaskDto Rank(TaskItem task);
    List<RankedTaskDto> Rank(IEnumerable<TaskItem> tasks);
}

public sealed class TaskRanker(ILogger<TaskRanker> logger) : ITaskRanker
{
    private static string ModelPath => Path.Combine(AppContext.BaseDirectory, "ML", "priority_model.zip");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _predictLock = new();
    private MLContext? _ml;
    private PredictionEngine<PriorityData, ScorePrediction>? _engine;

    public bool WasJustTrained { get; private set; }
    public double LastRSquared { get; private set; }

    public async Task EnsureModelAsync(CancellationToken ct = default)
    {
        if (_engine is not null) return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_engine is not null) return;

            _ml = new MLContext(seed: 42);
            WasJustTrained = false;

            if (File.Exists(ModelPath))
            {
                logger.LogInformation("Загрузка ML-модели из {Path}", ModelPath);
                var model = _ml.Model.Load(ModelPath, out _);
                _engine = _ml.Model.CreatePredictionEngine<PriorityData, ScorePrediction>(model);
            }
            else
            {
                logger.LogInformation("Обучение новой модели приоритета на синтетических данных...");
                var (model, r2) = ModelTrainer.TrainAndSave(_ml, ModelPath);
                _engine = _ml.Model.CreatePredictionEngine<PriorityData, ScorePrediction>(model);
                WasJustTrained = true;
                LastRSquared = r2;
                logger.LogInformation("Модель обучена. R2 = {R2:F3}, сохранена в {Path}", r2, ModelPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public RankedTaskDto Rank(TaskItem task)
    {
        var engine = _engine ?? throw new InvalidOperationException("Модель не инициализирована. Сначала вызовите EnsureModelAsync.");

        lock (_predictLock)
        {
            var prediction = engine.Predict(PriorityData.From(task));
            var score = Math.Clamp(prediction.Score, 0f, 100f);
            return new RankedTaskDto(task.Id, task.Title, (float)Math.Round(score, 1), BuildReason(task, score));
        }
    }

    public List<RankedTaskDto> Rank(IEnumerable<TaskItem> tasks) =>
        tasks.Select(Rank).OrderByDescending(r => r.Score).ToList();

    internal static string BuildReason(TaskItem task, float score)
    {
        var parts = new List<string>();

        parts.Add(task.DeadlineDays switch
        {
            <= 2 => $"критичный дедлайн: {task.DeadlineDays} дн.",
            <= 7 => $"близкий дедлайн: {task.DeadlineDays} дн.",
            <= 21 => $"средний дедлайн: {task.DeadlineDays} дн.",
            _ => $"дедлайн далеко: {task.DeadlineDays} дн."
        });

        var tags = PriorityData.SplitTags(task.Tags);
        if (tags.Any(t => t.Equals("urgent", StringComparison.OrdinalIgnoreCase)))
            parts.Add("метка urgent");
        if (tags.Length >= 3)
            parts.Add($"{tags.Length} тегов — широкий контекст");
        if (string.IsNullOrWhiteSpace(task.Assignee))
            parts.Add("нет исполнителя");

        parts.Add(score switch
        {
            >= 70 => "высокий приоритет",
            >= 40 => "средний приоритет",
            _ => "низкий приоритет"
        });

        return string.Join("; ", parts);
    }
}
