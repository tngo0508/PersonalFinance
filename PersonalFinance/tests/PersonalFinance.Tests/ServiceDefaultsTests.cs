using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PersonalFinance.Tests;

public class ServiceDefaultsTests
{
    [Fact]
    public void AddServiceDefaults_RegistersExpectedServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        // Act
        builder.AddServiceDefaults();
        using var app = builder.Build();

        // Assert - HealthChecksService
        var healthCheckService = app.Services.GetService<HealthCheckService>();
        Assert.NotNull(healthCheckService);

        // Assert - TracerProvider
        var tracerProvider = app.Services.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);

        // Assert - MeterProvider
        var meterProvider = app.Services.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddDefaultHealthChecks_RegistersLivenessCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddDefaultHealthChecks();
        using var host = builder.Build();

        // Assert
        var healthCheckService = host.Services.GetService<HealthCheckService>();
        Assert.NotNull(healthCheckService);
    }
}
