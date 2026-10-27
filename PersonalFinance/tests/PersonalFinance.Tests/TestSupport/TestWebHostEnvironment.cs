using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// The Web host environment for tests that register Identity or Razor services without a host: Development, named
/// after the Web project, with the test output directory as both roots and no files behind them.
/// </summary>
public sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = AppContext.BaseDirectory;

    public string ApplicationName { get; set; } = "PersonalFinance.Web";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public string EnvironmentName { get; set; } = "Development";
}
