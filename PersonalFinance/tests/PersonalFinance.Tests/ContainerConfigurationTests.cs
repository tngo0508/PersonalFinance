using System.IO;
using PersonalFinance.Data;
using Xunit;

namespace PersonalFinance.Tests;

public class ContainerConfigurationTests
{
    [Fact]
    public void Dockerignore_ExistsAndContainsEssentialExclusions()
    {
        var solutionRoot = DatabasePathHelper.GetSolutionRoot();
        var dockerignorePath = Path.Combine(solutionRoot, ".dockerignore");

        Assert.True(File.Exists(dockerignorePath), "Root .dockerignore file must exist.");

        var content = File.ReadAllText(dockerignorePath);

        // Verify exclusions for build artifacts
        Assert.Contains("bin/", content);
        Assert.Contains("obj/", content);

        // Verify exclusions for SQLite database files
        Assert.Contains("PersonalFinance.db", content);
        Assert.Contains("*.db", content);

        // Verify exclusions for secrets
        Assert.Contains("secrets.json", content);
        Assert.Contains(".env", content);
    }

    [Fact]
    public void ApiService_Dockerfile_ExistsAndHasProperConfiguration()
    {
        var solutionRoot = DatabasePathHelper.GetSolutionRoot();
        var dockerfilePath = Path.Combine(solutionRoot, "PersonalFinance", "src", "PersonalFinance.ApiService", "Dockerfile");

        Assert.True(File.Exists(dockerfilePath), "ApiService Dockerfile must exist.");

        var content = File.ReadAllText(dockerfilePath);

        // Verify multi-stage structure & .NET 10
        Assert.Contains("FROM", content);
        Assert.Contains("AS base", content);
        Assert.Contains("AS build", content);
        Assert.Contains("AS publish", content);
        Assert.Contains("AS final", content);
        Assert.Contains("10.0", content);

        // Verify non-root user & chiseled base
        Assert.Contains("USER $APP_UID", content);
        Assert.Contains("chiseled", content);

        // Verify port configuration
        Assert.Contains("8080", content);
        Assert.Contains("ASPNETCORE_HTTP_PORTS=8080", content);

        // Verify entrypoint
        Assert.Contains("PersonalFinance.ApiService.dll", content);
    }

    [Fact]
    public void Web_Dockerfile_ExistsAndHasProperConfiguration()
    {
        var solutionRoot = DatabasePathHelper.GetSolutionRoot();
        var dockerfilePath = Path.Combine(solutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Dockerfile");

        Assert.True(File.Exists(dockerfilePath), "Web Dockerfile must exist.");

        var content = File.ReadAllText(dockerfilePath);

        // Verify multi-stage structure & .NET 10
        Assert.Contains("FROM", content);
        Assert.Contains("AS base", content);
        Assert.Contains("AS build", content);
        Assert.Contains("AS publish", content);
        Assert.Contains("AS final", content);
        Assert.Contains("10.0", content);

        // Verify non-root user & chiseled base
        Assert.Contains("USER $APP_UID", content);

        // Verify port configuration
        Assert.Contains("8080", content);
        Assert.Contains("ASPNETCORE_HTTP_PORTS=8080", content);

        // Verify entrypoint
        Assert.Contains("PersonalFinance.Web.dll", content);
    }
}
