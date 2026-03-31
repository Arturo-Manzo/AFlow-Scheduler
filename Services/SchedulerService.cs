using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cronos;
using AScheduler.Queue;
using AScheduler.Data;
using AScheduler.Domain;
using TimeZoneConverter;

namespace AScheduler.Services
{
    public class SchedulerService : BackgroundService
    {
        private readonly IBoxRepository _boxRepository;
        private readonly ITaskQueue _queue;
        private readonly IConfiguration _config;
        private readonly ILogger<SchedulerService> _logger;

        public SchedulerService(IBoxRepository boxRepository, ITaskQueue queue, IConfiguration config, ILogger<SchedulerService> logger)
        {
            ArgumentNullException.ThrowIfNull(boxRepository);
            ArgumentNullException.ThrowIfNull(queue);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(logger);
            _boxRepository = boxRepository;
            _queue = queue;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetValue<int>("Scheduler:IntervalSeconds", 60);
            while (!stoppingToken.IsCancellationRequested)
            {
                await EvaluateBoxesAsync();
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }

        private async Task EvaluateBoxesAsync()
        {
            var boxes = await _boxRepository.GetActiveBoxesAsync();
            var now = DateTime.UtcNow;

            foreach (var box in boxes)
            {
                var scheduledOccurrence = GetLatestDueOccurrence(box, now);
                if (!scheduledOccurrence.HasValue)
                    continue;

                // DB is the single source of truth: any existing BoxRun (Pending, Running, or completed)
                // means this scheduled occurrence has already been handled.
                var alreadyExists = await _boxRepository.HasBoxRunForScheduledTimeAsync(box.Id, scheduledOccurrence.Value);
                if (alreadyExists)
                    continue;

                var boxRunId = await _boxRepository.CreateBoxRunAsync(box.Id, scheduledOccurrence.Value, TriggerSources.Scheduler, null);

                var enqueued = await _queue.EnqueueAsync(new BoxRunRequest
                {
                    BoxRunId = boxRunId,
                    BoxId = box.Id,
                    RequestedAt = now,
                    TriggerSource = TriggerSources.Scheduler,
                    ScheduledForUtc = scheduledOccurrence.Value
                });

                if (!enqueued)
                {
                    _logger.LogWarning("Failed to enqueue BoxRun {BoxRunId} for Box {BoxId}: already pending.", boxRunId, box.Id);
                }
            }

            await EvaluateManualQueueAsync();
        }

        private async Task EvaluateManualQueueAsync()
        {
            List<BoxQueueItem> pendingItems;
            try
            {
                pendingItems = await _boxRepository.GetPendingQueueItemsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read BoxExecutionQueue for manual items.");
                return;
            }

            foreach (var item in pendingItems)
            {
                await _boxRepository.MarkQueueItemAsync(item.QueueId, "Processing");

                var boxRunId = await _boxRepository.CreateBoxRunAsync(
                    item.BoxId, null, TriggerSources.Manual, item.RequestedByUserId);

                var enqueued = await _queue.EnqueueAsync(new BoxRunRequest
                {
                    BoxRunId = boxRunId,
                    BoxId = item.BoxId,
                    RequestedAt = item.CreatedAt,
                    ForceIgnoreDependencies = item.IgnoreDependencies,
                    ForceIgnoreSchedule = item.IgnoreSchedule,
                    RequestedByUserId = item.RequestedByUserId,
                    TriggerSource = TriggerSources.Manual
                });

                if (!enqueued)
                {
                    await _boxRepository.MarkQueueItemAsync(item.QueueId, "Failed");
                    _logger.LogWarning("Manual queue item {QueueId} for box {BoxId} could not be enqueued.", item.QueueId, item.BoxId);
                }
                else
                {
                    _logger.LogInformation("Manual queue item {QueueId} for box {BoxId} enqueued as BoxRun {BoxRunId}.", item.QueueId, item.BoxId, boxRunId);
                }
            }
        }

        private DateTime? GetLatestDueOccurrence(BoxDefinition box, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(box.CronExpression))
                return null;

            if (string.IsNullOrWhiteSpace(box.TimeZoneId))
                return null;

            try
            {
                var timeZone = TZConvert.GetTimeZoneInfo(box.TimeZoneId);
                var cron = CronExpression.Parse(box.CronExpression);
                var searchStart = utcNow.AddDays(-7).AddMinutes(-1);
                DateTime? latest = null;
                var next = cron.GetNextOccurrence(searchStart, timeZone);

                while (next.HasValue && next.Value <= utcNow)
                {
                    latest = next.Value;
                    next = cron.GetNextOccurrence(next.Value, timeZone);
                }

                if (!latest.HasValue)
                    return null;

                var createdAt = box.CreatedAtUtc.Kind switch
                {
                    DateTimeKind.Utc => box.CreatedAtUtc,
                    DateTimeKind.Local => box.CreatedAtUtc.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(box.CreatedAtUtc, DateTimeKind.Utc)
                };

                if (latest.Value < createdAt && latest.Value.Date < createdAt.Date)
                    return null;

                return latest.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Box {BoxId} has an invalid schedule: cron '{CronExpression}', time zone '{TimeZoneId}'.", box.Id, box.CronExpression, box.TimeZoneId);
                return null;
            }
        }
    }
}
