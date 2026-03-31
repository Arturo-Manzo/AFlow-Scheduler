using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AScheduler.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AScheduler.Tests;

[CollectionDefinition("ExecutionObservability", DisableParallelization = true)]
public class ExecutionObservabilityCollection : ICollectionFixture<ExecutionObservabilityFixture>
{
}

public class ExecutionObservabilityFixture : IAsyncLifetime
{
    private const string LocalDbServer = "(localdb)\\MSSQLLocalDB";

    public string DatabaseName { get; } = "AScheduler_Test_" + Guid.NewGuid().ToString("N");

    public string ConnectionString =>
        $"Server={LocalDbServer};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = ConnectionString
        })
        .Build();

    public async Task InitializeAsync()
    {
        await CreateDatabaseAsync();
        await CreateSchemaAsync();
        await SeedReferenceDataAsync();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync();
    }

    public async Task ResetExecutionsAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        const string sql = @"
            DELETE FROM dbo.TaskExecutions;
            DBCC CHECKIDENT ('dbo.TaskExecutions', RESEED, 0);";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        var master = $"Server={LocalDbServer};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();

        var sql = $"IF DB_ID(N'{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];";
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        var master = $"Server={LocalDbServer};Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();

        var sql = $@"
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        const string sql = @"
            IF OBJECT_ID('dbo.TaskExecutions', 'U') IS NOT NULL DROP TABLE dbo.TaskExecutions;
            IF OBJECT_ID('dbo.Tasks', 'U') IS NOT NULL DROP TABLE dbo.Tasks;
            IF OBJECT_ID('dbo.Boxes', 'U') IS NOT NULL DROP TABLE dbo.Boxes;
            IF OBJECT_ID('dbo.BoxRuns', 'U') IS NOT NULL DROP TABLE dbo.BoxRuns;
            IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;

            CREATE TABLE dbo.Users (
                UserId INT NOT NULL PRIMARY KEY,
                Username NVARCHAR(100) NOT NULL
            );

            CREATE TABLE dbo.Boxes (
                BoxId INT NOT NULL PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL,
                TimeZoneId NVARCHAR(100) NOT NULL
            );

            CREATE TABLE dbo.Tasks (
                TaskId INT NOT NULL PRIMARY KEY,
                BoxId INT NOT NULL,
                Name NVARCHAR(200) NOT NULL
            );

            CREATE TABLE dbo.BoxRuns (
                BoxRunId INT NOT NULL PRIMARY KEY,
                RequestedByUserId INT NULL
            );

            CREATE TABLE dbo.TaskExecutions (
                ExecutionId INT IDENTITY(1,1) PRIMARY KEY,
                TaskId INT NOT NULL,
                BoxRunId INT NULL,
                StartedAt DATETIME2 NOT NULL,
                EndedAt DATETIME2 NULL,
                Status NVARCHAR(20) NOT NULL,
                Output NVARCHAR(MAX) NULL,
                Error NVARCHAR(MAX) NULL,
                ExitCode INT NULL,
                StdOut NVARCHAR(MAX) NULL,
                StdErr NVARCHAR(MAX) NULL,
                TriggerSource NVARCHAR(20) NOT NULL,
                ScheduledForUtc DATETIME2 NULL,
                RequestedByUserId INT NULL,
                Reason NVARCHAR(500) NULL,
                CONSTRAINT CK_TaskExecutions_StatusLifecycle CHECK (
                    (Status = 'Running' AND StartedAt IS NOT NULL AND EndedAt IS NULL)
                    OR
                    (Status IN ('Success', 'Failed', 'Aborted') AND StartedAt IS NOT NULL AND EndedAt IS NOT NULL)
                )
            );

            CREATE UNIQUE INDEX UX_TaskExecutions_Running_BoxRunTask
                ON dbo.TaskExecutions(TaskId, BoxRunId)
                WHERE BoxRunId IS NOT NULL AND Status = 'Running';

            CREATE UNIQUE INDEX UX_TaskExecutions_Running_ForceStartTask
                ON dbo.TaskExecutions(TaskId, TriggerSource)
                WHERE BoxRunId IS NULL AND TriggerSource = 'ForceStart' AND Status = 'Running';

            CREATE INDEX IX_TaskExecutions_TaskId_Started
                ON dbo.TaskExecutions(TaskId, StartedAt DESC);

            CREATE INDEX IX_TaskExecutions_Status_StartedAt
                ON dbo.TaskExecutions(Status, StartedAt);";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedReferenceDataAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO dbo.Users(UserId, Username) VALUES (1, 'operator1');
            INSERT INTO dbo.Boxes(BoxId, Name, TimeZoneId) VALUES (10, 'Box A', 'Etc/UTC');
            INSERT INTO dbo.Tasks(TaskId, BoxId, Name) VALUES (100, 10, 'Task A');
            INSERT INTO dbo.BoxRuns(BoxRunId, RequestedByUserId) VALUES (1000, 1);";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[Collection("ExecutionObservability")]
public class ExecutionRepositoryIntegrationTests
{
    private readonly ExecutionObservabilityFixture _fixture;

    public ExecutionRepositoryIntegrationTests(ExecutionObservabilityFixture fixture)
    {
        _fixture = fixture;
    }

    private ExecutionRepository CreateRepository() => new(_fixture.Configuration);

    [Fact]
    public async Task SuccessfulExecution_CreatesRecord_WithSuccessAndTimestamps()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();
        var startedAt = new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc);
        var endedAt = startedAt.AddSeconds(4);

        // Act
        var executionId = await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt,
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "manual run");

        await repository.CompleteExecutionAsync(
            executionId,
            endedAtUtc: endedAt,
            status: "Success",
            output: "done",
            error: "",
            exitCode: 0,
            stdOut: "done",
            stdErr: "");

        var last = await repository.GetLastExecutionForTaskAsync(100);

        // Assert
        Assert.NotNull(last);
        Assert.Equal(executionId, last!.ExecutionId);
        Assert.Equal("Success", last.Status);
        Assert.Equal(startedAt, last.StartedAt);
        Assert.Equal(endedAt, last.EndedAt);
    }

    [Fact]
    public async Task FailedExecution_StoresFailedStatusAndErrorMessage()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();
        var startedAt = new DateTime(2026, 3, 30, 12, 10, 0, DateTimeKind.Utc);
        var endedAt = startedAt.AddSeconds(2);
        const string errorMessage = "Unhandled exception: test failure";

        // Act
        var executionId = await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt,
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "failure simulation");

        await repository.CompleteExecutionAsync(
            executionId,
            endedAtUtc: endedAt,
            status: "Failed",
            output: "",
            error: errorMessage,
            exitCode: -1,
            stdOut: "",
            stdErr: errorMessage);

        var history = await repository.GetExecutionsForTaskAsync(100);

        // Assert
        Assert.Single(history);
        Assert.Equal("Failed", history[0].Status);
        Assert.Equal(errorMessage, history[0].Error);
        Assert.Equal(endedAt, history[0].EndedAt);
    }

    [Fact]
    public async Task RunningState_IsPersistedBeforeCompletion()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();
        var startedAt = new DateTime(2026, 3, 30, 12, 20, 0, DateTimeKind.Utc);

        // Act
        var executionId = await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt,
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "running-state-check");

        var running = await repository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-5));

        // Assert
        var current = Assert.Single(running);
        Assert.Equal(executionId, current.ExecutionId);
        Assert.Equal("Running", current.Status);
        Assert.Equal(startedAt, current.StartedAt);
        Assert.Null(current.EndedAt);
    }

    [Fact]
    public async Task ConcurrencyControl_SameTaskTriggeredTwice_DoesNotCreateTwoRunningExecutions()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();
        var startedAt = new DateTime(2026, 3, 30, 12, 30, 0, DateTimeKind.Utc);

        // Act
        await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt,
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "first trigger");

        async Task CreateDuplicateAsync() => await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt.AddSeconds(1),
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "duplicate trigger");

        // Assert
        await Assert.ThrowsAsync<SqlException>(CreateDuplicateAsync);
        var running = await repository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-5));
        Assert.Single(running);
    }

    [Fact]
    public async Task DataConsistency_RunningHasNoEndTime_AndFinishedHasEndTime()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();
        var startedAt = new DateTime(2026, 3, 30, 12, 40, 0, DateTimeKind.Utc);
        var endedAt = startedAt.AddSeconds(6);

        // Act
        var executionId = await repository.CreateExecutionAsync(
            taskId: 100,
            boxRunId: 1000,
            startedAtUtc: startedAt,
            triggerSource: "Manual",
            scheduledForUtc: null,
            requestedByUserId: 1,
            reason: "consistency check");

        var runningBeforeFinish = await repository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-5));

        await repository.CompleteExecutionAsync(
            executionId,
            endedAtUtc: endedAt,
            status: "Success",
            output: "ok",
            error: "",
            exitCode: 0,
            stdOut: "ok",
            stdErr: "");

        var last = await repository.GetLastExecutionForTaskAsync(100);

        // Assert
        Assert.Single(runningBeforeFinish);
        Assert.Null(runningBeforeFinish[0].EndedAt);

        Assert.NotNull(last);
        Assert.Equal("Success", last!.Status);
        Assert.NotNull(last.EndedAt);
        Assert.Equal(endedAt, last.EndedAt);
    }

    [Fact]
    public async Task GetExecutions_ReturnsHistoryByTaskAndDateRange_AndLastExecution()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();

        var t1 = new DateTime(2026, 3, 30, 13, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 3, 30, 13, 10, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 3, 30, 13, 20, 0, DateTimeKind.Utc);

        var e1 = await repository.CreateExecutionAsync(100, 1000, t1, "Manual", null, 1, "run1");
        await repository.CompleteExecutionAsync(e1, t1.AddSeconds(1), "Success", "", "", 0, "", "");

        var e2 = await repository.CreateExecutionAsync(100, 1000, t2, "Manual", null, 1, "run2");
        await repository.CompleteExecutionAsync(e2, t2.AddSeconds(1), "Failed", "", "boom", -1, "", "boom");

        var e3 = await repository.CreateExecutionAsync(100, 1000, t3, "Manual", null, 1, "run3");
        await repository.CompleteExecutionAsync(e3, t3.AddSeconds(1), "Success", "", "", 0, "", "");

        // Act
        var range = await repository.GetExecutionsForTaskAsync(100, t2, t3.AddSeconds(5));
        var last = await repository.GetLastExecutionForTaskAsync(100);

        // Assert
        Assert.Equal(2, range.Count);
        Assert.Equal(e3, range[0].ExecutionId);
        Assert.Equal(e2, range[1].ExecutionId);

        Assert.NotNull(last);
        Assert.Equal(e3, last!.ExecutionId);
        Assert.Equal("Success", last.Status);
    }

    [Fact]
    public async Task AbortRunningExecutions_OnStartup_MarksAllRunningAsAborted()
    {
        // Arrange
        await _fixture.ResetExecutionsAsync();
        var repository = CreateRepository();

        var startedAt = new DateTime(2026, 3, 30, 14, 0, 0, DateTimeKind.Utc);
        const string abortReason = "Execution interrupted due to server restart.";

        // Create two running executions (use different BoxRunIds to satisfy the unique index)
        var e1 = await repository.CreateExecutionAsync(100, 1000, startedAt, "Manual", null, 1, "run1");
        var e2 = await repository.CreateExecutionAsync(100, null, startedAt.AddSeconds(5), "ForceStart", null, 1, "run2");

        var abortedAt = startedAt.AddMinutes(30);

        // Act — simulates startup recovery
        var count = await repository.AbortRunningExecutionsAsync(abortedAt, abortReason);

        // Assert
        Assert.Equal(2, count);

        var history = await repository.GetExecutionsForTaskAsync(100);
        Assert.All(history, r =>
        {
            Assert.Equal("Aborted", r.Status);
            Assert.Equal(abortedAt, r.EndedAt);
            Assert.Equal(abortReason, r.Error);
            Assert.Equal(abortReason, r.StdErr);
        });

        // Confirm no Running executions remain
        var running = await repository.GetRunningExecutionsAsync(DateTime.UtcNow.AddMinutes(-1));
        Assert.Empty(running);
    }
}
