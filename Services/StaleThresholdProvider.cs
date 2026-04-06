using System.Collections.Concurrent;
using CHRONIQ.Data;

namespace CHRONIQ.Services;

/// <summary>
/// Computes per-task stale thresholds using formula:
/// <c>max(globalFloor, min(avg * multiplier, globalMax))</c>.
/// Results are cached in-memory with a configurable TTL.
/// </summary>
public sealed class StaleThresholdProvider : IStaleThresholdProvider
{
    private readonly IExecutionRepository _executionRepository;
    private readonly double _globalFloorMinutes;
    private readonly double _globalMaxMinutes;
    private readonly double _multiplier;
    private readonly int _sampleSize;
    private readonly TimeSpan _cacheTtl;

    private readonly ConcurrentDictionary<int, (double ThresholdMinutes, DateTime ExpiresAtUtc)> _cache = new();

    public StaleThresholdProvider(IExecutionRepository executionRepository, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(executionRepository);
        ArgumentNullException.ThrowIfNull(configuration);

        _executionRepository = executionRepository;
        _globalFloorMinutes = Math.Max(1, configuration.GetValue<int>("WorkerPool:StaleExecutionThresholdMinutes", 15));
        _globalMaxMinutes = Math.Max(_globalFloorMinutes, configuration.GetValue<int>("WorkerPool:StaleThresholdMaxMinutes", 120));
        _multiplier = Math.Max(1.0, configuration.GetValue<double>("WorkerPool:StaleThresholdMultiplier", 3.0));
        _sampleSize = Math.Max(1, configuration.GetValue<int>("WorkerPool:StaleThresholdSampleSize", 10));
        _cacheTtl = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue<int>("WorkerPool:StaleThresholdCacheTtlMinutes", 5)));
    }

    public async Task<double> GetStaleThresholdMinutesAsync(int taskId)
    {
        var now = DateTime.UtcNow;

        if (_cache.TryGetValue(taskId, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.ThresholdMinutes;
        }

        var avgSeconds = await _executionRepository.GetAverageExecutionDurationSecondsAsync(taskId, _sampleSize);
        double threshold;

        if (avgSeconds is null or <= 0)
        {
            threshold = _globalFloorMinutes;
        }
        else
        {
            var dynamicMinutes = (avgSeconds.Value / 60.0) * _multiplier;
            threshold = Math.Max(_globalFloorMinutes, Math.Min(dynamicMinutes, _globalMaxMinutes));
        }

        _cache[taskId] = (threshold, now + _cacheTtl);
        return threshold;
    }

    public async Task<bool> IsStaleAsync(int taskId, DateTime startedAtUtc)
    {
        var thresholdMinutes = await GetStaleThresholdMinutesAsync(taskId);
        return startedAtUtc < DateTime.UtcNow.AddMinutes(-thresholdMinutes);
    }
}
