using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonalFinance.ApiService.Features.Admin;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Tests.BudgetTracker;

/// <summary>
/// Calls the real ApiService pipeline through the same Refit client the Web app registers (default settings), so
/// the wire format of <see cref="SaveCategoryRequest"/> is proven to bind. Refit writes enums as strings by default
/// and the ApiService's JSON options do not read them, so the request's <see cref="SaveCategoryRequest.Kind"/> pins
/// its number format; without that, every "Add Category" from the Web ends in a 400 ("The request was invalid.").
/// </summary>
[Collection("ApiServiceHost")]
public class BudgetTrackerWireFormatTests : IClassFixture<BudgetTrackerWireFormatTests.ApiServiceHost>
{
    private const string InternalKey = "budget-tracker-wire-format-secret";

    private readonly ApiServiceHost _host;

    public BudgetTrackerWireFormatTests(ApiServiceHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task CreateCategory_ThroughTheWebRefitClient_IsCreated()
    {
        using HttpClient httpClient = HttpClientFor($"wire-format-{Guid.NewGuid():N}");
        IBudgetTrackerApi api = RestService.For<IBudgetTrackerApi>(httpClient);

        BudgetCategoryDto created = await api.CreateCategoryAsync(
            new SaveCategoryRequest { Name = "Investment", Kind = BudgetCategoryKind.Expense, SortOrder = 110 });

        Assert.Equal("Investment", created.Name);
        Assert.Equal(BudgetCategoryKind.Expense, created.Kind);
        Assert.Equal(110, created.SortOrder);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task UpdateCategory_ThroughTheWebRefitClient_IsUpdated()
    {
        using HttpClient httpClient = HttpClientFor($"wire-format-{Guid.NewGuid():N}");
        IBudgetTrackerApi api = RestService.For<IBudgetTrackerApi>(httpClient);
        BudgetCategoryDto created = await api.CreateCategoryAsync(
            new SaveCategoryRequest { Name = "Side income", Kind = BudgetCategoryKind.Income, SortOrder = 40 });

        BudgetCategoryDto updated = await api.UpdateCategoryAsync(created.Id,
            new SaveCategoryRequest { Name = "Freelance", Kind = BudgetCategoryKind.Income, SortOrder = 50 });

        Assert.Equal("Freelance", updated.Name);
        Assert.Equal(BudgetCategoryKind.Income, updated.Kind);
        Assert.Equal(50, updated.SortOrder);
    }

    [Fact]
    public async Task CreateCategory_WithANumericKind_StillBinds()
    {
        using HttpClient httpClient = HttpClientFor($"wire-format-{Guid.NewGuid():N}");
        using var content = new StringContent("""{"name":"Pets","kind":2,"sortOrder":10}""", Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response =
            await httpClient.PostAsync(new Uri("/api/budget-tracker/categories", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Pets", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("kind").GetInt32());
    }

    private HttpClient HttpClientFor(string userId)
    {
        HttpClient client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Internal-Key", InternalKey);
        return client;
    }

    /// <summary>The ApiService entry point, InMemory database, shared secret configured.</summary>
    public sealed class ApiServiceHost : WebApplicationFactory<AdminController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                $"InMemory:BudgetTrackerWireFormat_{Guid.NewGuid():N}");
            builder.UseSetting("Internal:ApiKey", InternalKey);
        }
    }
}
