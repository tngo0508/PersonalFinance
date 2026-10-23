using System.Diagnostics;
using PersonalFinance.ApiService.Controllers;
using Xunit;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// Runs the built ApiService as a real process with <c>--migrate-only</c>, so the exit code is what a
/// Container Apps job, Docker or CI would see (#108).
/// </summary>
public class ApiServiceProcessExitCodeTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task MigrateOnly_StartupFailure_ExitsNonZeroAndLogsFatal()
    {
        // A malformed connection string fails startup before any network call, so the test is fast and deterministic.
        (int ExitCode, string Output) result = await RunApiServiceAsync(new Dictionary<string, string>
        {
            ["Database__Provider"] = "AzureSql",
            ["ConnectionStrings__DefaultConnection"] = "Server=127.0.0.1,1;Not A Real Keyword=1"
        });

        Assert.True(result.ExitCode != 0, $"Expected a non-zero exit code. Output:{Environment.NewLine}{result.Output}");
        Assert.Contains("Application terminated unexpectedly.", result.Output);
    }

    [Fact]
    public async Task MigrateOnly_Success_ExitsZero()
    {
        (int ExitCode, string Output) result = await RunApiServiceAsync(new Dictionary<string, string>
        {
            ["Database__Provider"] = "InMemory",
            ["ConnectionStrings__DefaultConnection"] = "InMemory:exit-code-test"
        });

        Assert.True(result.ExitCode == 0, $"Expected exit code 0. Output:{Environment.NewLine}{result.Output}");
        Assert.DoesNotContain("Application terminated unexpectedly.", result.Output);
    }

    private static async Task<(int ExitCode, string Output)> RunApiServiceAsync(Dictionary<string, string> environment)
    {
        string apiServiceDll = typeof(AdminController).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(apiServiceDll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(apiServiceDll);
        startInfo.ArgumentList.Add("--migrate-only");

        // Production skips user secrets; clear provider settings inherited from the test run (CI sets InMemory).
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        startInfo.Environment.Remove("DATABASE_PROVIDER");
        startInfo.Environment.Remove("DB_PROVIDER");
        startInfo.Environment.Remove("MIGRATE_ONLY");
        foreach ((string? key, string? value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"ApiService --migrate-only did not exit within {ProcessTimeout.TotalSeconds}s.");
        }

        return (process.ExitCode, await stdout + await stderr);
    }
}
