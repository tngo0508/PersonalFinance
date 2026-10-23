using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PersonalFinance.Tests.Infrastructure;

public class ServiceDefaultsTests
{
    [Fact]
    public void AddServiceDefaults_RegistersExpectedServices()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddServiceDefaults();
        using WebApplication app = builder.Build();

        // Assert - HealthChecksService
        HealthCheckService? healthCheckService = app.Services.GetService<HealthCheckService>();
        Assert.NotNull(healthCheckService);

        // Assert - TracerProvider
        TracerProvider? tracerProvider = app.Services.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);

        // Assert - MeterProvider
        MeterProvider? meterProvider = app.Services.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddDefaultHealthChecks_RegistersLivenessCheck()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddDefaultHealthChecks();
        using IHost host = builder.Build();

        // Assert
        HealthCheckService? healthCheckService = host.Services.GetService<HealthCheckService>();
        Assert.NotNull(healthCheckService);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void MapDefaultEndpoints_MapsHealthEndpoints_InAllEnvironments(string environmentName)
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.AddServiceDefaults();
        WebApplication app = builder.Build();

        // Act
        app.MapDefaultEndpoints();

        // Assert - verify endpoints are registered in endpoint data source
        ICollection<EndpointDataSource> endpointDataSources = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources;
        var endpoints = endpointDataSources.SelectMany(ds => ds.Endpoints).ToList();

        Assert.Contains(endpoints, e => e is Microsoft.AspNetCore.Routing.RouteEndpoint route && route.RoutePattern.RawText == "/health");
        Assert.Contains(endpoints, e => e is Microsoft.AspNetCore.Routing.RouteEndpoint route && route.RoutePattern.RawText == "/alive");
    }
}
