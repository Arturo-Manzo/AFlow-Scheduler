using AScheduler.Api.Dtos;

namespace AScheduler.Api.Services;

/// <summary>
/// Provides a single search entry point for boxes and tasks.
/// The implementation can later be swapped from database search to Elasticsearch without changing controllers.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Searches boxes, tasks, or both depending on the selected scope.
    /// </summary>
    /// <param name="query">Free text search query.</param>
    /// <param name="scope">Scope selector: all, box, or task.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <returns>Ordered search results for the requested scope.</returns>
    Task<List<SearchResultDto>> SearchAsync(string query, string scope, int limit);
}