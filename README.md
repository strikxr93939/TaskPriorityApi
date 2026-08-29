# Task Priority API

REST API для приоритизации задач: принимает список задач (CSV или JSON), обучает ML-модель на синтетических данных и возвращает для каждой задачи `priority_score` (0–100) с объяснением.

## Стек

- .NET 8, ASP.NET Core
- ML.NET 3 (FastTree, регрессия)
- EF Core 8 + SQLite
- Swagger UI (`/swagger`)
- Docker / docker-compose, xUnit, GitHub Actions (CI: restore → build → test)

## API

| Метод | Путь | Описание |
|---|---|---|
| POST | `/api/tasks/upload` | Загрузка задач: CSV (multipart, поле `file`) или JSON-массив |
| POST | `/api/tasks/rank` | Ранжирование всех задач, возвращает `{ id, title, score, reason }` по убыванию score |
| GET | `/api/tasks/{id}` | Детали задачи |
| GET | `/api/tasks/stats` | Средний score по тегам |

Заголовок CSV: `title,deadline_days,assignee,tags` (теги разделяются `;`).

### Примеры

```bash
# JSON
curl -X POST http://localhost:8080/api/tasks/upload \
  -H "Content-Type: application/json" \
  -d '[{"title":"Fix prod outage","deadlineDays":1,"assignee":"alice","tags":"urgent;bug"}]'

# CSV
curl -X POST http://localhost:8080/api/tasks/upload -F "file=@tasks.csv;type=text/csv"

# Ранжирование
curl -X POST http://localhost:8080/api/tasks/rank
```

Ответ `rank`:

```json
[
  {
    "id": 1,
    "title": "Fix prod outage",
    "score": 99.3,
    "reason": "критичный дедлайн: 1 дн.; метка urgent; высокий приоритет"
  }
]
```

Коды ответов: `200` — успех, `400` — некорректный CSV/JSON, `404` — задача не найдена, `500` — внутренняя ошибка (все в формате `{ "error": "..." }`).

## Как работает ML

- Признаки: `DeadlineDays`, `AssigneeKnown`, `TagCount`, `HasUrgentTag`, `TitleLength`
- Тренер: FastTree (100 деревьев) на 800 синтетических записях с шумом; правило разметки: дедлайн ≤ 2 дней → score 85–100, ≤ 7 → 60–85, ≤ 21 → 35–60, далее 10–35
- При первом старте (если `ML/priority_model.zip` отсутствует) модель обучается, сохраняется на диск, а её R² пишется в таблицу `ModelMetrics`
- Итоговый score ограничивается диапазоном 0–100; `reason` строится из признаков задачи (дедлайн, urgent-метка, отсутствие исполнителя, число тегов)

## Схема БД (EF Core, SQLite)

- `Task { Id, Title, DeadlineDays, Assignee, Tags, Score, CreatedAt }`
- `ModelMetrics { Id, ModelVersion, Accuracy, TrainedAt }`

## Запуск

```bash
# локально
dotnet run --project src/TaskPriorityApi
# Swagger: http://localhost:5080/swagger

# docker-compose (порт 8080, SQLite на named volume)
docker compose up --build
```

## Тесты и CI

```bash
dotnet test
```

GitHub Actions (`.github/workflows/ci.yml`) гоняет restore → build → test на .NET 8 при каждом push/PR.
