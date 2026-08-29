namespace TaskPriorityApi.Models;

public class ModelMetrics
{
    public int Id { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
}
