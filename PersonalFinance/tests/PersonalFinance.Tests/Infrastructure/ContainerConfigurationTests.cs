using System.IO;
using PersonalFinance.Tests.TestSupport;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

public class ContainerConfigurationTests
{
    [Fact]
    public void Dockerignore_ExistsAndContainsEssentialExclusions()
    {
        string solutionRoot = RepositoryRoot.Find();
        string dockerignorePath = Path.Combine(solutionRoot, ".dockerignore");

        Assert.True(File.Exists(dockerignorePath), "Root .dockerignore file must exist.");

        string content = File.ReadAllText(dockerignorePath);

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

    [Theory]
    [InlineData("PersonalFinance.ApiService")]
    [InlineData("PersonalFinance.Web")]
    public void Dockerfile_CopiesSharedMsBuildFilesBeforeRestore(string project)
    {
        // TargetFramework lives in PersonalFinance/Directory.Build.props; a Docker build context without it
        // fails restore with NETSDK1013 ("The TargetFramework value '' was not recognized").
        string solutionRoot = RepositoryRoot.Find();
        string dockerfile = File.ReadAllText(Path.Combine(solutionRoot, "PersonalFinance", "src", project, "Dockerfile"));
        int restoreAt = dockerfile.IndexOf("dotnet restore", StringComparison.Ordinal);

        foreach (string? file in new[] { "PersonalFinance/Directory.Build.props", "PersonalFinance/Directory.Packages.props" })
        {
            int copiedAt = dockerfile.IndexOf(file, StringComparison.Ordinal);
            Assert.True(copiedAt >= 0 && copiedAt < restoreAt, $"{project} Dockerfile must COPY {file} before dotnet restore.");
        }
    }

    [Fact]
    public void ApiService_Dockerfile_ExistsAndHasProperConfiguration()
    {
        string solutionRoot = RepositoryRoot.Find();
        string dockerfilePath = Path.Combine(solutionRoot, "PersonalFinance", "src", "PersonalFinance.ApiService", "Dockerfile");

        Assert.True(File.Exists(dockerfilePath), "ApiService Dockerfile must exist.");

        string content = File.ReadAllText(dockerfilePath);

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
        string solutionRoot = RepositoryRoot.Find();
        string dockerfilePath = Path.Combine(solutionRoot, "PersonalFinance", "src", "PersonalFinance.Web", "Dockerfile");

        Assert.True(File.Exists(dockerfilePath), "Web Dockerfile must exist.");

        string content = File.ReadAllText(dockerfilePath);

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
