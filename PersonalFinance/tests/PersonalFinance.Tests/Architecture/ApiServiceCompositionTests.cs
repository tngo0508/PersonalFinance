using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.ApiService.Infrastructure;

namespace PersonalFinance.Tests.Architecture;

/// <summary>
/// The ApiService container, built the way the deployed host builds it, resolves every Feature interface and every
/// Validator. A Feature that forgets its registration, or a Validator that FluentValidation's assembly scan skips
/// (internal types are skipped unless asked for), fails here instead of at the first request in production.
/// </summary>
[Collection("ApiServiceHost")]
public class ApiServiceCompositionTests : IClassFixture<ApiServiceCompositionTests.ApiServiceHost>
{
    private static readonly Assembly ApiServiceAssembly = typeof(AdminController).Assembly;

    /// <summary>Public interfaces that are not DI seams, with the reason.</summary>
    private static readonly Dictionary<Type, string> NotResolvedByDesign = new()
    {
        [typeof(IBudgetMonthReader)] = "the Data Source provider picks a reader by source (ADR 0002); the readers are registered as themselves"
    };

    private readonly ApiServiceHost _host;

    public ApiServiceCompositionTests(ApiServiceHost host)
    {
        _host = host;
    }

    [Fact]
    public void EveryPublicInterface_Resolves()
    {
        List<Type> interfaces = ApiServiceAssembly.GetExportedTypes()
            .Where(t => t.IsInterface && !NotResolvedByDesign.ContainsKey(t))
            .ToList();
        Assert.NotEmpty(interfaces);

        using IServiceScope scope = _host.Services.CreateScope();
        List<string> unresolved = interfaces
            .Where(t => scope.ServiceProvider.GetService(t) is null)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0,
            "Interfaces the ApiService host does not register (add them to the Feature's registration):\n  " +
            string.Join("\n  ", unresolved));
    }

    [Fact]
    public void EveryValidator_Resolves_IncludingInternalOnes()
    {
        List<Type> validators = ApiServiceAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && ValidatedType(t) is not null)
            .ToList();
        Assert.NotEmpty(validators);
        Assert.Contains(validators, v => !v.IsPublic); // proves the scan includes internal types

        using IServiceScope scope = _host.Services.CreateScope();
        List<string> unresolved = validators
            .Where(v => scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(ValidatedType(v)!)) is null
                        || scope.ServiceProvider.GetService(v) is null)
            .Select(v => v.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0,
            "Validators the ApiService host does not register:\n  " + string.Join("\n  ", unresolved));
    }

    private static Type? ValidatedType(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }
        return null;
    }

    /// <summary>The ApiService entry point with an InMemory database, as the other ApiService host tests use it.</summary>
    public sealed class ApiServiceHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"InMemory:Composition_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", "composition-test-secret");
        }
    }
}
