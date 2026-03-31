using AScheduler.Api.Dtos;

namespace AScheduler.Services;

public interface IBoxRunMetricsService
{
    Task<BoxRunMetricsDto?> GetMetricsAsync(int boxRunId);
}