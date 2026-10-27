using FluentValidation.Results;
using PersonalFinance.ApiService.Features.Feedback;
using PersonalFinance.ApiService.Features.Feedback.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Feedback;

/// <summary>
/// The Admin Feedback inbox's status filter: no status or a known Feedback Status is valid; any other value is refused
/// with the unknown-status key and message, on the <c>status</c> query field.
/// </summary>
public class FeedbackListFilterValidatorTests
{
    private readonly FeedbackListFilterValidator _validator = new();

    [Fact]
    public void NoStatus_IsValid()
    {
        Assert.True(_validator.Validate(new FeedbackListFilter(null)).IsValid);
    }

    [Theory]
    [InlineData(FeedbackStatus.New)]
    [InlineData(FeedbackStatus.Reviewed)]
    [InlineData(FeedbackStatus.Archived)]
    public void AKnownStatus_IsValid(FeedbackStatus status)
    {
        Assert.True(_validator.Validate(new FeedbackListFilter(status)).IsValid);
    }

    [Fact]
    public void AnUnknownStatus_IsRefused_WithTheUnknownStatusMessage()
    {
        ValidationResult result = _validator.Validate(new FeedbackListFilter((FeedbackStatus)99));

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(ApiErrorKeys.Feedback.StatusUnknown, failure.ErrorCode);
        Assert.Equal(FeedbackMessages.UnknownStatus, failure.ErrorMessage);
        Assert.Equal("status", failure.PropertyName);
    }
}
