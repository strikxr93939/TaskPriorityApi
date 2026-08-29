using Microsoft.EntityFrameworkCore;
using TaskPriorityApi.Data;
using TaskPriorityApi.Models;
using TaskPriorityApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Task Priority API",
        Version = "v1",
        Description = "Ранжирование задач по приоритету (ML.NET, score 0-100) + объяснение"
    });
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

builder.Services.AddDbContext<TaskContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=tasks.db"));

builder.Services.AddSingleton<ITaskRanker, TaskRanker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskContext>();
    db.Database.EnsureCreated();

    var ranker = scope.ServiceProvider.GetRequiredService<ITaskRanker>();
    await ranker.EnsureModelAsync();
    if (ranker.WasJustTrained)
    {
        db.Metrics.Add(new ModelMetrics
        {
            ModelVersion = $"v{DateTime.UtcNow:yyyyMMddHHmmss}",
            Accuracy = ranker.LastRSquared,
            TrainedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
}));

app.MapControllers();

app.Run();
