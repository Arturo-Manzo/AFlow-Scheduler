using System.Collections.Generic;
using System.Threading.Tasks;
using AScheduler.Api.Controllers;
using AScheduler.Api.Dtos;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AScheduler.Tests;

public class ExecutionHistoryControllerTests
{
    private static ExecutionHistoryController BuildController(
        IExecutionRepository? executionRepository = null,
        ITaskRepository? taskRepository = null,
        IBoxRepository? boxRepository = null,
        IConfiguration? configuration = null,
        IStaleThresholdProvider? staleThresholdProvider = null)
    {
        var config = configuration ?? new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkerPool:StaleExecutionThresholdMinutes"] = "15"
            })
            .Build();

        return new ExecutionHistoryController(
            executionRepository ?? Mock.Of<IExecutionRepository>(),
            taskRepository ?? Mock.Of<ITaskRepository>(),
            boxRepository ?? Mock.Of<IBoxRepository>(),
            config,
            staleThresholdProvider ?? Mock.Of<IStaleThresholdProvider>());
    }

    [Fact]
    public async Task GetForTask_TaskNotFound_ReturnsNotFound()
    {
        var taskRepoMock = new Mock<ITaskRepository>(MockBehavior.Strict);
        taskRepoMock.Setup(r => r.GetByIdAsync(42)).ReturnsAsync((TaskDefinition?)null);

        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        var boxRepoMock = new Mock<IBoxRepository>(MockBehavior.Loose);

        var controller = BuildController(execRepoMock.Object, taskRepoMock.Object, boxRepoMock.Object);

        var result = await controller.GetForTask(42);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFound.Value!);
        Assert.False(response.Success);
        Assert.Equal("TASK_NOT_FOUND", response.ErrorCode);
    }

    [Fact]
    public async Task GetForTask_ReturnsHistoryWithDurationAndError()
    {
        var taskRepoMock = new Mock<ITaskRepository>(MockBehavior.Strict);
        taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new TaskDefinition
        {
            Id = 1,
            BoxId = 10,
            Name = "Task A",
            Command = "cmd",
            TaskType = TaskType.Exe
        });

        var boxRepoMock = new Mock<IBoxRepository>(MockBehavior.Strict);
        boxRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(new BoxDefinition
        {
            Id = 10,
            Name = "Box A",
            DepartmentId = null  // No department
        });

        var startedAt = System.DateTime.UtcNow.AddMinutes(-2);
        var endedAt = System.DateTime.UtcNow;
        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetExecutionsForTaskAsync(1, null, null)).ReturnsAsync(new List<ExecutionRepository.ExecutionRecord>
        {
            new()
            {
                ExecutionId = 7,
                TaskId = 1,
                TaskName = "Task A",
                BoxId = 10,
                BoxName = "Box A",
                StartedAt = startedAt,
                EndedAt = endedAt,
                Status = "Failed",
                ExitCode = 1,
                Error = "boom",
                StdErr = "stack",
                TriggerSource = "Manual"
            }
        });

        var controller = BuildController(execRepoMock.Object, taskRepoMock.Object, boxRepoMock.Object);

        var result = await controller.GetForTask(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ExecutionDto>>>(ok.Value!);
        Assert.Single(response.Data);
        Assert.Equal(7, response.Data[0].ExecutionId);
        Assert.Equal("boom", response.Data[0].ErrorMessage);
        Assert.True(response.Data[0].DurationSeconds >= 119);
    }

    [Fact]
    public async Task GetLastForTask_NoHistory_ReturnsNotFound()
    {
        var taskRepoMock = new Mock<ITaskRepository>(MockBehavior.Strict);
        taskRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new TaskDefinition
        {
            Id = 5,
            BoxId = 2,
            Name = "Task B",
            Command = "cmd",
            TaskType = TaskType.Exe
        });

        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetLastExecutionForTaskAsync(5)).ReturnsAsync((ExecutionRepository.ExecutionRecord?)null);

        var boxRepoMock = new Mock<IBoxRepository>(MockBehavior.Strict);
        boxRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(new BoxDefinition
        {
            Id = 2,
            Name = "Box B",
            DepartmentId = null
        });

        var controller = BuildController(execRepoMock.Object, taskRepoMock.Object, boxRepoMock.Object);

        var result = await controller.GetLastForTask(5);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFound.Value!);
        Assert.Equal("NO_EXECUTION_HISTORY", response.ErrorCode);
    }

    [Fact]
    public async Task GetRunning_ReturnsRunningExecutions()
    {
        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetRunningExecutionsAsync(It.IsAny<System.DateTime>())).ReturnsAsync(new List<ExecutionRepository.ExecutionRecord>
        {
            new()
            {
                ExecutionId = 11,
                TaskId = 3,
                TaskName = "Long Task",
                BoxId = 1,
                BoxName = "Ops",
                StartedAt = System.DateTime.UtcNow.AddMinutes(-20),
                Status = "Running",
                TriggerSource = "ForceStart",
                IsStale = true
            }
        });

        var staleMock = new Mock<IStaleThresholdProvider>();
        staleMock.Setup(s => s.IsStaleAsync(3, It.IsAny<System.DateTime>())).ReturnsAsync(true);
        staleMock.Setup(s => s.GetStaleThresholdMinutesAsync(3)).ReturnsAsync(15.0);

        var controller = BuildController(execRepoMock.Object, staleThresholdProvider: staleMock.Object);

        var result = await controller.GetRunning();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<RunningExecutionDto>>>(ok.Value!);
        Assert.Single(response.Data);
        Assert.True(response.Data[0].IsStale);
        Assert.Equal("Running", response.Data[0].Status);
        Assert.Equal(15.0, response.Data[0].StaleThresholdMinutes);
    }
}

