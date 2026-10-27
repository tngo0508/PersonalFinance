using System.Text.RegularExpressions;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// Every Feature but Admin (localized API errors spec §2.4) rejects only with an
/// <c>ApiErrorDto</c> that carries an <c>ApiErrorKeys</c> key, which Web translates. These guards keep a converted
/// Feature from slipping back to an English body: no literal or message-rewrapping <c>BadRequest</c>, no service
/// throwing a framework exception for a controller to rewrap, and no validator error code that is not a key constant.
/// </summary>
public class ApiErrorArchitectureTests
{
    /// <summary>The one Feature whose rejections stay English: the Admin area is not localized (ADR 0017).</summary>
    private const string UnlocalizedFeature = "Admin";

    private static readonly Regex LiteralBadRequest = new(
        @"BadRequest\(\s*""|BadRequest\(\s*\$""|BadRequest\(\s*new\s*\{|BadRequest\(\s*[a-zA-Z_.]+\.(Error)?Message\s*\)",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex RewrappedException = new(
        @"throw new (ArgumentException|KeyNotFoundException|InvalidOperationException)\(",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex LiteralErrorCode = new(
        @"WithErrorCode\(\s*""",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    [Fact]
    public void ConvertedFeatures_ReturnNoLiteralBadRequest()
    {
        List<string> offenders = Offenders(LiteralBadRequest, ConvertedSourceFiles());

        Assert.True(offenders.Count == 0,
            "Return BadRequest(validation.ToApiError()) or throw ApiErrorException with a key instead:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void ConvertedServices_ThrowNoExceptionForTheControllerToRewrap()
    {
        List<string> offenders = Offenders(RewrappedException,
            ConvertedSourceFiles().Where(path => Path.GetFileName(path).EndsWith("Service.cs", StringComparison.Ordinal)));

        Assert.True(offenders.Count == 0,
            "Throw ApiErrorException with an ApiErrorKeys key for a rejection instead:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ConvertedValidators_UseApiErrorKeysAsErrorCodes()
    {
        List<string> offenders = Offenders(LiteralErrorCode, ConvertedSourceFiles());

        Assert.True(offenders.Count == 0,
            "Pass an ApiErrorKeys constant to WithErrorCode instead of a literal:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("return BadRequest(\"x\");")]
    [InlineData("return BadRequest($\"x {y}\");")]
    [InlineData("return BadRequest(new { error = \"x\" });")]
    [InlineData("return BadRequest(ex.Message);")]
    [InlineData("return BadRequest(result.ErrorMessage);")]
    public void TheBadRequestPattern_CatchesEveryLiteralShape(string line)
    {
        Assert.Matches(LiteralBadRequest, line);
    }

    [Theory]
    [InlineData("return BadRequest(validation.ToApiError());")]
    [InlineData("return NotFound();")]
    public void TheBadRequestPattern_LeavesKeyedRejectionsAlone(string line)
    {
        Assert.DoesNotMatch(LiteralBadRequest, line);
    }

    [Fact]
    public void TheScan_CoversEveryFeatureButAdmin()
    {
        List<string> features = ConvertedFeatureFolders().Select(Path.GetFileName).OfType<string>().ToList();

        Assert.Contains("GoogleDrive", features);
        Assert.Contains("MoneyPriorities", features);
        Assert.DoesNotContain(UnlocalizedFeature, features);
    }

    /// <summary>Every folder under <c>Features/</c> except <see cref="UnlocalizedFeature"/>, so a new Feature is scanned from its first commit.</summary>
    private static IEnumerable<string> ConvertedFeatureFolders() =>
        Directory.EnumerateDirectories(Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.ApiService"), "Features"))
            .Where(folder => !string.Equals(Path.GetFileName(folder), UnlocalizedFeature, StringComparison.Ordinal));

    private static IEnumerable<string> ConvertedSourceFiles() => ConvertedFeatureFolders().SelectMany(SourceFiles);

    private static List<string> Offenders(Regex pattern, IEnumerable<string> files) =>
        files
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, number: index + 1)))
            .Where(entry => pattern.IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(RepositoryRoot.Find(), entry.path)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"));
}
