namespace TaskPriorityApi.ML;

public static class SyntheticDataGenerator
{
    private static readonly string[] TagPool = { "bug", "feature", "docs", "infra", "refactor", "security", "test", "support", "release", "urgent" };
    private static readonly string[] Assignees = { "alice", "bob", "carol", "dave", "eve" };
    private static readonly string[] TitleTemplates = { "Fix bug in", "Add feature to", "Update docs for", "Refactor", "Investigate issue in", "Optimize", "Implement", "Review" };
    private static readonly string[] Modules = { "auth", "billing", "api", "frontend", "database", "cache", "queue", "reports" };

    public static List<PriorityData> Generate(int count = 800, int seed = 42)
    {
        var rnd = new Random(seed);
        var list = new List<PriorityData>(count);

        for (int i = 0; i < count; i++)
        {
            int deadline = rnd.Next(0, 61);
            bool urgent = deadline <= 2 || rnd.NextDouble() < 0.15;

            var tags = new List<string>();
            if (urgent) tags.Add("urgent");
            int tagCount = rnd.Next(1, 4);
            while (tags.Count < tagCount)
            {
                var tag = TagPool[rnd.Next(TagPool.Length)];
                if (!tags.Contains(tag)) tags.Add(tag);
            }

            bool hasAssignee = rnd.NextDouble() > 0.15;
            string title = $"{TitleTemplates[rnd.Next(TitleTemplates.Length)]} {Modules[rnd.Next(Modules.Length)]}";

            list.Add(new PriorityData
            {
                DeadlineDays = deadline,
                AssigneeKnown = hasAssignee ? 1f : 0f,
                TagCount = tags.Count,
                HasUrgentTag = urgent ? 1f : 0f,
                TitleLength = title.Length,
                Score = TrueScore(deadline, tags.Count, urgent, hasAssignee, title.Length, rnd)
            });
        }

        return list;
    }

    private static float TrueScore(int deadline, int tagCount, bool urgent, bool hasAssignee, int titleLength, Random rnd)
    {
        float score = deadline switch
        {
            <= 2 => 90f,
            <= 7 => 70f,
            <= 21 => 45f,
            _ => 20f
        };
        score += (tagCount - 1) * 3f;
        if (urgent) score += 6f;
        if (!hasAssignee) score += 4f;
        score += (titleLength % 10) * 0.3f;
        score += (float)(rnd.NextDouble() * 10 - 5);
        return Math.Clamp(score, 0f, 100f);
    }
}
