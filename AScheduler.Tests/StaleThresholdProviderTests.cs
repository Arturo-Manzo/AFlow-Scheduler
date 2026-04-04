using System.Collections.Generic;
using System.Threading.Tasks;
using AScheduler.Data;
using AScheduler.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AScheduler.Tests;

public class StaleThresholdProviderTests
{
    private static (StaleThresholdProvider provider, Mock<IExecutionRepository> repoMock) Build(
        int globalFloor = 15,
        double multiplier = 3.0,
        int maxMinutes = 120,
        int sampleSize = 10,
        int cacheTtlMinutes = 5)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkerPool:StaleExecutionThresholdMinutes"] = globalFloor.ToString(),
                ["WorkerPool:StaleThresholdMultiplier"] = multiplier.ToString(),
                ["WorkerPool:StaleThresholdMaxMinutes"] = maxMinutes.ToString(),
                ["WorkerPool:StaleThresholdSampleSize"] = sampleSize.ToString(),
                ["WorkerPool:StaleThresholdCacheTtlMinutes"] = cacheTtlMinutes.ToString()
            })
            .Build();

        var repoMock = new Mock<IExecutionRepository>();
        var provider = new StaleThresholdProvider(repoMock.Object, config);
        return (provider, repoMock);
    }

    [Fact]
    public async Task NoHistory_ReturnGlobalFloor()
    {
        var (provider, repoMock) = Build(globalFloor: 15);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync((double?)null);

        var result = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(15.0, result);
    }

    [Fact]
    public async Task ShortTask_FlooredToGlobalMinimum()
    {
        // avg = 2 seconds → 2/60 * 3 = 0.1 min → should be floored to 15
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(2.0);

        var result = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(15.0, result);
    }

    [Fact]
    public async Task LongTask_UsesMultipliedAverage()
    {
        // avg = 600 seconds (10m) → 10 * 3 = 30 min
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(600.0);

        var result = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(30.0, result);
    }

    [Fact]
    public async Task VeryLongTask_CappedAtMax()
    {
        // avg = 3600 seconds (60m) → 60 * 3 = 180 min → capped at 120
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0, maxMinutes: 120);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(3600.0);

        var result = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(120.0, result);
    }

    [Fact]
    public async Task CachedResult_DoesNotHitRepoTwice()
    {
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(600.0);

        var result1 = await provider.GetStaleThresholdMinutesAsync(1);
        var result2 = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(result1, result2);
        repoMock.Verify(r => r.GetAverageExecutionDurationSecondsAsync(1, 10), Times.Once);
    }

    [Fact]
    public async Task IsStale_RecentExecution_NotStale()
    {
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync((double?)null);

        // Started 5 minutes ago, threshold is 15 → not stale
        var startedAt = System.DateTime.UtcNow.AddMinutes(-5);
        var result = await provider.IsStaleAsync(1, startedAt);

        Assert.False(result);
    }

    [Fact]
    public async Task IsStale_OldExecution_IsStale()
    {
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync((double?)null);

        // Started 20 minutes ago, threshold is 15 → stale
        var startedAt = System.DateTime.UtcNow.AddMinutes(-20);
        var result = await provider.IsStaleAsync(1, startedAt);

        Assert.True(result);
    }

    [Fact]
    public async Task DifferentTasks_GetDifferentThresholds()
    {
        var (provider, repoMock) = Build(globalFloor: 15, multiplier: 3.0);
        // Task 1: short task (2 sec avg → floor of 15)
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(2.0);
        // Task 2: long task (600 sec avg → 30 min)
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(2, 10)).ReturnsAsync(600.0);

        var result1 = await provider.GetStaleThresholdMinutesAsync(1);
        var result2 = await provider.GetStaleThresholdMinutesAsync(2);

        Assert.Equal(15.0, result1);
        Assert.Equal(30.0, result2);
    }

    [Fact]
    public async Task ZeroAvg_ReturnGlobalFloor()
    {
        var (provider, repoMock) = Build(globalFloor: 15);
        repoMock.Setup(r => r.GetAverageExecutionDurationSecondsAsync(1, 10)).ReturnsAsync(0.0);

        var result = await provider.GetStaleThresholdMinutesAsync(1);

        Assert.Equal(15.0, result);
    }
}
