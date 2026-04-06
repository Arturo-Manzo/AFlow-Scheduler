using CHRONIQ.Api.Dtos;

namespace CHRONIQ.Services;

public interface IBoxRunMetricsService
{
    Task<BoxRunMetricsDto?> GetMetricsAsync(int boxRunId);
}