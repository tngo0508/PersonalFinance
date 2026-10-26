using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.Tests.Infrastructure;

/// <summary>
/// The ApiService side of ADR 0017: an <see cref="ApiErrorException"/> or a failed <see cref="ValidationResult"/>
/// becomes an <see cref="ApiErrorDto"/> carrying a localization key, the English message and typed arguments.
/// </summary>
public class ApiErrorProductionTests
{
    private static ExceptionContext RunFilter(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, []) { Exception = exception };
        new ApiErrorExceptionFilter(NullLogger<ApiErrorExceptionFilter>.Instance).OnException(context);
        return context;
    }

    [Fact]
    public void Filter_RendersApiErrorException_AsItsStatusAndDto()
    {
        ExceptionContext context = RunFilter(
            new ApiErrorException("Community.Error.PollNotFound", "Poll not found or inactive.", statusCode: 404));

        Assert.True(context.ExceptionHandled);
        ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(404, result.StatusCode);
        ApiErrorDto dto = Assert.IsType<ApiErrorDto>(result.Value);
        ApiErrorEntryDto entry = Assert.Single(dto.Errors);
        Assert.Equal("Community.Error.PollNotFound", entry.Key);
        Assert.Equal("Poll not found or inactive.", entry.Message);
        Assert.Null(entry.Field);
        Assert.Null(entry.Args);
    }

    [Fact]
    public void Filter_DefaultsTo400_AndCopiesFieldAndArgs()
    {
        ExceptionContext context = RunFilter(
            new ApiErrorException("K", "msg", "Amount", ErrorArgs.Number("max", 5m)));

        ObjectResult result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        ApiErrorEntryDto entry = Assert.Single(Assert.IsType<ApiErrorDto>(result.Value).Errors);
        Assert.Equal("Amount", entry.Field);
        Assert.NotNull(entry.Args);
        Assert.Equal("5", entry.Args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Number, entry.Args["max"].Kind);
    }

    [Fact]
    public void Filter_LeavesOtherExceptionsAlone()
    {
        ExceptionContext context = RunFilter(new InvalidOperationException("boom"));

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void ToApiError_MapsCodeMessageFieldAndState()
    {
        ValidationResult validation = new([
            new ValidationFailure("MonthlyAmount", "msg")
            {
                ErrorCode = "K",
                CustomState = ErrorArgs.MoneyRange(0m, 1_000_000m, "USD")
            }
        ]);

        ApiErrorEntryDto entry = Assert.Single(validation.ToApiError().Errors);

        Assert.Equal("K", entry.Key);
        Assert.Equal("msg", entry.Message);
        Assert.Equal("MonthlyAmount", entry.Field);
        Assert.NotNull(entry.Args);
        Assert.Equal("0", entry.Args["min"].Value);
        Assert.Equal("1000000", entry.Args["max"].Value);
        Assert.Equal(ApiErrorArgKind.Money, entry.Args["max"].Kind);
        Assert.Equal("USD", entry.Args["max"].Currency);
    }

    [Fact]
    public void ToApiError_WithoutState_HasNullArgs_AndEmptyPropertyIsNullField()
    {
        ValidationResult validation = new([
            new ValidationFailure(string.Empty, "whole request") { ErrorCode = "A" },
            new ValidationFailure("Name", "other state") { ErrorCode = "B", CustomState = "not a dictionary" }
        ]);

        List<ApiErrorEntryDto> errors = validation.ToApiError().Errors;

        Assert.Equal(2, errors.Count);
        Assert.Equal("A", errors[0].Key);
        Assert.Null(errors[0].Field);
        Assert.Null(errors[0].Args);
        Assert.Equal("Name", errors[1].Field);
        Assert.Null(errors[1].Args);
    }

    [Fact]
    public void ErrorArgs_Number_WritesInvariantWithoutTrailingZeros()
    {
        Assert.Equal("2000", ErrorArgs.Number("max", 2000.00m)["max"].Value);
        Assert.Equal("2.5", ErrorArgs.Number("x", 2.5m)["x"].Value);
    }

    [Fact]
    public void ErrorArgs_TextAndMoney_CarryKindAndCurrency()
    {
        ApiErrorArgDto text = ErrorArgs.Text("name", "Rent")["name"];
        Assert.Equal(ApiErrorArgKind.Text, text.Kind);
        Assert.Equal("Rent", text.Value);
        Assert.Null(text.Currency);

        ApiErrorArgDto money = ErrorArgs.Money("limit", 10_000_000.00m, "VND")["limit"];
        Assert.Equal(ApiErrorArgKind.Money, money.Kind);
        Assert.Equal("10000000", money.Value);
        Assert.Equal("VND", money.Currency);
    }
}
