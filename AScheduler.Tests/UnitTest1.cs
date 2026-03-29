using System.Collections.Generic;
using System.Threading.Tasks;
using AScheduler.Api.Controllers;
using AScheduler.Api.Dtos;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Domain;
using AScheduler.Queue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AScheduler.Tests;

public class TasksControllerTests
{
    private static TasksController BuildController(
        ITaskRepository? taskRepo = null,
        IExecutionRepository? execRepo = null,
        ITaskQueue? queue = null,
        IAuditLogService? audit = null)
    {
        return new TasksController(
            taskRepo  ?? Mock.Of<ITaskRepository>(),
            execRepo  ?? Mock.Of<IExecutionRepository>(),
            queue     ?? Mock.Of<ITaskQueue>(),
            audit     ?? Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<TasksController>>());
    }

    [Fact]
    public async Task GetTaskExecutions_TaskNotFound_ReturnsNotFound()
    {
        var taskRepoMock = new Mock<ITaskRepository>(MockBehavior.Strict);
        taskRepoMock.Setup(r => r.GetById(It.IsAny<int>())).ReturnsAsync((TaskDefinition?)null);

        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);

        var controller = BuildController(taskRepo: taskRepoMock.Object, execRepo: execRepoMock.Object);

        var result = await controller.GetTaskExecutions(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(notFound.Value!);

        Assert.False(response.Success);
        Assert.Equal("TASK_NOT_FOUND", response.ErrorCode);

        taskRepoMock.Verify(r => r.GetById(999), Times.Once);
        execRepoMock.Verify(r => r.GetExecutionsForTaskAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetTaskExecutions_TaskFound_ReturnsExecutionList()
    {
        var taskRepoMock = new Mock<ITaskRepository>(MockBehavior.Strict);
        taskRepoMock.Setup(r => r.GetById(1))
            .ReturnsAsync(new TaskDefinition
            {
                Id            = 1,
                Name          = "Test",
                Command       = "cmd",
                TaskType      = TaskType.Exe,
                CronExpression = "* * * * *",
                AllowParallel = false
            });

        var record = new ExecutionRepository.ExecutionRecord
        {
            ExecutionId = 10,
            TaskId      = 1,
            TaskName    = "Test",
            StartedAt   = System.DateTime.UtcNow.AddMinutes(-5),
            EndedAt     = System.DateTime.UtcNow,
            Status      = "Success",
            ExitCode    = 0,
            Output      = "OK",
            Error       = string.Empty
        };

        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetExecutionsForTaskAsync(1))
            .ReturnsAsync(new List<ExecutionRepository.ExecutionRecord> { record });

        var controller = BuildController(taskRepo: taskRepoMock.Object, execRepo: execRepoMock.Object);

        var result = await controller.GetTaskExecutions(1);

        var ok       = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ExecutionDto>>>(ok.Value!);

        Assert.True(response.Success);
        Assert.Single(response.Data);
        Assert.Equal(1,  response.Data[0].TaskId);
        Assert.Equal(10, response.Data[0].ExecutionId);

        taskRepoMock.Verify(r => r.GetById(1), Times.Once);
        execRepoMock.Verify(r => r.GetExecutionsForTaskAsync(1), Times.Once);
    }
}

public class ExecutionHistoryControllerTests
{
    [Fact]
    public async Task GetLatestExecutions_ReturnsData()
    {
        var records = new List<ExecutionRepository.ExecutionRecord>
        {
            new ExecutionRepository.ExecutionRecord
            {
                ExecutionId = 1,
                TaskId      = 1,
                TaskName    = "Test",
                StartedAt   = System.DateTime.UtcNow.AddMinutes(-1),
                EndedAt     = System.DateTime.UtcNow,
                Status      = "Success",
                ExitCode    = 0,
                Output      = "OK",
                Error       = string.Empty
            }
        };

        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetLatestExecutionsAsync(20)).ReturnsAsync(records);

        var controller = new ExecutionHistoryController(execRepoMock.Object);

        var result = await controller.GetLatestExecutions();

        var ok       = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ExecutionDto>>>(ok.Value!);

        Assert.True(response.Success);
        Assert.Single(response.Data);
        Assert.Equal("Test", response.Data[0].TaskName);

        execRepoMock.Verify(r => r.GetLatestExecutionsAsync(20), Times.Once);
    }

    [Fact]
    public async Task GetExecutionHistoryForTask_NoRecords_ReturnsNotFound()
    {
        var execRepoMock = new Mock<IExecutionRepository>(MockBehavior.Strict);
        execRepoMock.Setup(r => r.GetExecutionsForTaskAsync(42))
            .ReturnsAsync(new List<ExecutionRepository.ExecutionRecord>());

        var controller = new ExecutionHistoryController(execRepoMock.Object);

        var result = await controller.GetExecutionHistoryForTask(42);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<ExecutionDto>>>(notFound.Value!);

        Assert.False(response.Success);

        execRepoMock.Verify(r => r.GetExecutionsForTaskAsync(42), Times.Once);
    }
}

