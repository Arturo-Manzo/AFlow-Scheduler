using System.Reflection;
using CHRONIQ.Api.Controllers;
using CHRONIQ.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CHRONIQ.Tests;

public sealed class HealthDashboardContractTests
{
    [Fact]
    public void HealthDashboardController_IsAdminOnly()
    {
        var authorize = typeof(HealthDashboardController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault();
        var route = typeof(HealthDashboardController)
            .GetCustomAttributes<RouteAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
        Assert.NotNull(route);
        Assert.Equal("api/health-dashboard", route!.Template);
    }

    [Fact]
    public void HealthDashboardDtos_DoNotExposeSecretOrConnectionFields()
    {
        var dtoTypes = new[]
        {
            typeof(HealthDashboardDto),
            typeof(HealthSummaryDto),
            typeof(ReadinessCheckDto),
            typeof(ApplicationLogDto),
            typeof(SystemStatusDto)
        };

        var forbiddenTerms = new[]
        {
            "ConnectionString",
            "Password",
            "Secret",
            "Token",
            "ApiKey"
        };

        var propertyNames = dtoTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name)
            .ToList();

        foreach (var forbiddenTerm in forbiddenTerms)
        {
            Assert.DoesNotContain(propertyNames, name => name.Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase));
        }
    }
}
