using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Models.Research;
using ApiException = Refit.ApiException;
using RefitSettings = Refit.RefitSettings;
using ResearchController = PersonalFinance.Web.Controllers.ResearchController;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Web Research page reads each dataset from ApiService through <see cref="IResearchApi"/>. A dataset that has
/// never loaded (503), or that cannot be read, shows that tab's Unavailable State instead of an empty chart.
/// </summary>
public class ResearchWebControllerTests
{
    private static readonly SectorsSnapshotDto Snapshot = new()
    {
        AsOf = new DateOnly(2025, 12, 31),
        Periods = ["2024", "2025"],
        Companies = [new SectorsCompanyDto { Cik = 320193, Name = "APPLE INC" }, new SectorsCompanyDto { Cik = 789019, Name = "MICROSOFT CORP" }],
        Values =
        [
            new SectorsPeriodDto { Period = "2024", Ranked = [new SectorsRankedValueDto { Company = 0, PublicFloat = 2m }] },
            new SectorsPeriodDto
            {
                Period = "2025",
                Ranked =
                [
                    new SectorsRankedValueDto { Company = 1, PublicFloat = 3_000m },
                    new SectorsRankedValueDto { Company = 0, PublicFloat = 2_600m },
                ],
                RestTotal = 40m,
                RestCount = 7,
            },
        ],
    };

    private static ResearchController CreateController(IResearchApi api) =>
        new(api, NullLogger<ResearchController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ResearchViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<ResearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<ApiException> ApiFailure(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return await ApiException.Create(new HttpRequestMessage(HttpMethod.Get, "https://api.local/api/Research/sectors"),
            HttpMethod.Get, response, new RefitSettings());
    }

    [Fact]
    public void Controller_RequiresSignIn()
    {
        Assert.NotNull(typeof(ResearchController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Index_HoldsTheSectorsSnapshot_WithTheLatestYearRankedForTheTable()
    {
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Returns(nameof(IResearchApi.GetSectorsAsync), Snapshot);

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.False(model.SectorsUnavailable);
        Assert.Same(Snapshot, model.Sectors);
        Assert.Equal(new DateOnly(2025, 12, 31), model.SectorsAsOf);
        Assert.Equal("2025", model.SectorsLatestPeriod!.Period);
        Assert.Equal([(1, "MICROSOFT CORP", 3_000m), (2, "APPLE INC", 2_600m)],
            model.SectorsTableRows.Select(r => (r.Rank, r.Name, r.PublicFloat)));
    }

    [Fact]
    public async Task Index_WhenTheSectorsDataHasNeverLoaded_MarksTheTabUnavailable()
    {
        string body = JsonSerializer.Serialize(new ResearchUnavailableDto { Reason = "The Sectors data has not loaded yet." },
            JsonSerializerOptions.Web);
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), await ApiFailure(HttpStatusCode.ServiceUnavailable, body));

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.SectorsUnavailable);
        Assert.Null(model.Sectors);
        Assert.Null(model.SectorsAsOf);
        Assert.Empty(model.SectorsTableRows);
        Assert.Equal("Research", model.SectorsUnavailableState.RetryController);
        Assert.False(string.IsNullOrWhiteSpace(model.SectorsUnavailableState.Heading));
    }

    [Fact]
    public async Task Index_WhenApiServiceIsUnreachable_MarksTheTabUnavailable_WithoutExceptionDetails()
    {
        (IResearchApi api, ApiProxy<IResearchApi> _) = ApiProxy.Create<IResearchApi>();

        ResearchViewModel model = ViewModelOf(await CreateController(api).Index());

        Assert.True(model.SectorsUnavailable);
        Assert.DoesNotContain("unreachable", model.SectorsUnavailableState.Heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_WhenTheRequestIsCancelled_DoesNotSwallowTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        (IResearchApi api, ApiProxy<IResearchApi> proxy) = ApiProxy.Create<IResearchApi>();
        proxy.Throws(nameof(IResearchApi.GetSectorsAsync), new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateController(api).Index(cts.Token));
    }
}
