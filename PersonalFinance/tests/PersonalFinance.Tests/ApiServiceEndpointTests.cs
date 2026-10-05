using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.Constants;
using Scalar.AspNetCore;
using Xunit;

namespace PersonalFinance.Tests;

public class ApiServiceEndpointTests
{
    [Fact]
    public void ApiService_DevelopmentEnvironment_RegistersOpenApiAndHealthEndpoints()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.AddServiceDefaults();
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = $"{AppVersion.ApplicationName} API Service";
                document.Info.Version = AppVersion.Current;
                return Task.CompletedTask;
            });
        });

        var app = builder.Build();

        // Act - register the standard endpoints mapped by ApiService Program.cs
        app.MapDefaultEndpoints();
        app.MapGet("/", () => Results.Ok(new
        {
            Application = AppVersion.ApplicationName,
            Version = AppVersion.Current,
            Status = "Online"
        }));

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        // Assert - endpoints inspection
        var endpointDataSources = ((IEndpointRouteBuilder)app).DataSources;
        var endpoints = endpointDataSources.SelectMany(ds => ds.Endpoints).ToList();

        // 1. Verify health & liveness endpoints
        Assert.Contains(endpoints, e => e is RouteEndpoint route && route.RoutePattern.RawText == "/health");
        Assert.Contains(endpoints, e => e is RouteEndpoint route && route.RoutePattern.RawText == "/alive");

        // 2. Verify root status endpoint
        Assert.Contains(endpoints, e => e is RouteEndpoint route && route.RoutePattern.RawText == "/");

        // 3. Verify OpenAPI spec endpoint
        Assert.Contains(endpoints, e => e is RouteEndpoint route && route.RoutePattern.RawText != null && route.RoutePattern.RawText.Contains("openapi"));

        // 4. Verify Scalar API documentation endpoint
        Assert.Contains(endpoints, e => e is RouteEndpoint route && route.RoutePattern.RawText != null && route.RoutePattern.RawText.Contains("scalar"));
    }
}
