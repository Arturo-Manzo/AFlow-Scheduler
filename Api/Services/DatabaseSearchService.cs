using AScheduler.Api.Dtos;
using AScheduler.Data;

namespace AScheduler.Api.Services;

/// <summary>
/// Database-backed implementation of unified search.
/// This service isolates the search contract so the application can adopt Elasticsearch later without changing API consumers.
/// </summary>
public class DatabaseSearchService(IBoxRepository boxRepository, ITaskRepository taskRepository) : ISearchService
{
    /// <inheritdoc />
    public async Task<List<SearchResultDto>> SearchAsync(string query, string scope, int limit)
    {
        ArgumentNullException.ThrowIfNull(boxRepository);
        ArgumentNullException.ThrowIfNull(taskRepository);

        var normalizedQuery = (query ?? string.Empty).Trim();
        var normalizedScope = NormalizeScope(scope);
        var safeLimit = Math.Max(1, Math.Min(limit, 100));

        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return new List<SearchResultDto>();

        var results = new List<SearchResultDto>();

        if (normalizedScope is "all" or "box")
        {
            var boxes = await boxRepository.SearchAsync(normalizedQuery, safeLimit);
            results.AddRange(boxes.Select(box => new SearchResultDto
            {
                ResultType = "box",
                BoxId = box.BoxId,
                Title = box.Name,
                Description = box.Description,
                BoxName = box.Name,
                TimeZoneId = box.TimeZoneId,
                CreatedAt = box.CreatedAtUtc,
                Enabled = box.Enabled,
                ActiveTaskCount = box.ActiveTaskCount
            }));
        }

        if (normalizedScope is "all" or "task")
        {
            var tasks = await taskRepository.SearchAsync(normalizedQuery, safeLimit);
            results.AddRange(tasks.Select(task => new SearchResultDto
            {
                ResultType = "task",
                BoxId = task.BoxId,
                TaskId = task.TaskId,
                Title = task.TaskName,
                Description = task.TaskDescription,
                BoxName = task.BoxName,
                Command = task.Command,
                TaskType = task.TaskType,
                CreatedAt = task.CreatedAtUtc,
                Enabled = task.TaskEnabled,
                BoxEnabled = task.BoxEnabled
            }));
        }

        return results
            .OrderBy(result => GetPriority(result, normalizedQuery))
            .ThenBy(result => result.ResultType)
            .ThenBy(result => result.Title)
            .Take(safeLimit)
            .ToList();
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = (scope ?? "all").Trim().ToLowerInvariant();
        return normalized is "box" or "task" ? normalized : "all";
    }

    private static int GetPriority(SearchResultDto result, string query)
    {
        if (result.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (!string.IsNullOrWhiteSpace(result.BoxName) && result.BoxName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (!string.IsNullOrWhiteSpace(result.Command) && result.Command.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 2;

        return 3;
    }
}