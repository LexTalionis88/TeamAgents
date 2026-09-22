namespace AgentClient.Workflow;

/// <summary>
/// Проверяет форму и минимальную глубину декомпозиции плана Manager.
/// </summary>
internal static class TaskPlanValidator
{
    /// <summary>
    /// Определяет минимальное число срезов по числу независимых concern-групп
    /// в исходной постановке, не зная предметную область задачи.
    /// </summary>
    public static int GetMinimumWorkItems(string requirements, int maximumItems)
    {
        var normalized = requirements.ToLowerInvariant();
        var concernGroups = new[]
        {
            new[] { "api", "endpoint", "controller", "маршрут" },
            new[] { "database", "postgres", "postgresql", "ef core", "storage", "базу", "база данных", "хранилищ" },
            new[] { "redis", "cache", "кэш", "кеш", "rabbitmq", "очеред" },
            new[] { "docker", "compose", "container", "deployment", "dockerfile", "контейнер", "развёртыв" },
            new[] { "test", "tests", "testing", "тест", "провер" },
            new[] { "readme", "documentation", "документац", "документ" },
        };

        var concernCount = concernGroups.Count(group =>
            group.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)));

        var minimum = concernCount switch
        {
            0 or 1 => 1,
            2 => 2,
            _ => 3,
        };

        return Math.Clamp(minimum, 1, maximumItems);
    }

    /// <summary>
    /// Проверяет обязательные поля, порядок зависимостей и глубину плана.
    /// </summary>
    public static bool IsValid(
        TaskPlan? taskPlan,
        int maximumItems,
        int minimumItems)
    {
        if (taskPlan is null ||
            string.IsNullOrWhiteSpace(taskPlan.Summary) ||
            taskPlan.Items is not { Count: > 0 } ||
            taskPlan.Items.Count < minimumItems ||
            taskPlan.Items.Count > maximumItems)
        {
            return false;
        }

        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in taskPlan.Items)
        {
            if (item is null ||
                string.IsNullOrWhiteSpace(item.Id) ||
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Objective) ||
                item.AcceptanceCriteria is not { Count: > 0 } ||
                !knownIds.Add(item.Id) ||
                item.Dependencies is null ||
                item.Dependencies.Any(dependency =>
                    dependency is null ||
                    dependency.Equals(item.Id, StringComparison.OrdinalIgnoreCase) ||
                    !knownIds.Contains(dependency)))
            {
                return false;
            }
        }

        return true;
    }
}
