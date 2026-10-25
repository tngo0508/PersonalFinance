using System.Text.Json;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The age range travels as a number between the page, the Web app and the ApiService. Refit's default serializer
/// writes enums as camelCase strings, which the ApiService cannot bind, so the enum pins its own wire format.
/// </summary>
public class RetirementAgeBandWireFormatTests
{
    [Fact]
    public void TheWebAppsRefitSerializer_SendsTheAgeBandAsANumber()
    {
        JsonSerializerOptions refitOptions = SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions();
        var request = new SubmitPriorityWalkRequest { AgeBand = RetirementAgeBand.Age60To63 };

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(request, refitOptions));

        JsonElement ageBand = json.RootElement.GetProperty("ageBand");
        Assert.Equal(JsonValueKind.Number, ageBand.ValueKind);
        Assert.Equal(3, ageBand.GetInt32());
    }

    [Fact]
    public void TheApiServicesDefaultSerializer_ReadsTheNumberBack()
    {
        SubmitPriorityWalkRequest? request = JsonSerializer.Deserialize<SubmitPriorityWalkRequest>(
            """{"ageBand":2}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(RetirementAgeBand.Age50To59Or64Up, request?.AgeBand);
    }
}
