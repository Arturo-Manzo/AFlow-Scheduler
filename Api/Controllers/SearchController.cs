using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AScheduler.Api.Controllers;

/// <summary>
/// Unified search entry point for locating boxes and tasks across the platform.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController(ISearchService searchService) : ControllerBase
{
    /// <summary>
    /// Searches boxes, tasks, or both using a free-text query.
    /// </summary>
    /// <param name="q">Free-text query.</param>
    /// <param name="scope">Search scope: all, box, or task.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    [HttpGet]
    [Authorize(Roles = "Admin,Operator,Viewer")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string scope = "all", [FromQuery] int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Enter at least 2 characters to search.",
                ErrorCode = "SEARCH_QUERY_TOO_SHORT"
            });
        }

        var results = await searchService.SearchAsync(q, scope, limit);
        return Ok(new ApiResponse<List<SearchResultDto>> { Success = true, Data = results });
    }
}