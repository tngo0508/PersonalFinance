using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PersonalFinance.Data;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Web.Controllers;

namespace PersonalFinance.Tests.Infrastructure;

public class ModelValidationConventionTests
{
    private static readonly Assembly[] ProductAssemblies =
    [
        typeof(LegalController).Assembly,
        typeof(AppDbContext).Assembly,
        typeof(AppVersion).Assembly
    ];

    [Fact]
    public void BooleanProperties_DoNotUseRequiredAttribute()
    {
        // [Required] on a non-nullable bool is always satisfied (false is a value), so it never rejects
        // an unchecked box. Enforce acceptance explicitly (ModelState error) or use [Range(typeof(bool), "true", "true")].
        var offenders = ProductAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.PropertyType == typeof(bool) && property.IsDefined(typeof(RequiredAttribute), inherit: true))
            .Select(property => $"{property.DeclaringType?.FullName}.{property.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, $"[Required] has no effect on bool properties: {string.Join(", ", offenders)}");
    }
}
