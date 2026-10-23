using FluentValidation.Results;
using PersonalFinance.ApiService.Features.AccountLifecycle.Validators;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AccountDeletion;

namespace PersonalFinance.Tests.AccountLifecycle;

/// <summary>
/// A cancel token must look like one the service issued (Base64Url) before it is hashed and looked up; anything
/// else is refused with the invalid-link message, which the controller returns as 400.
/// </summary>
public class CancelDeletionRequestValidatorTests
{
    private readonly CancelDeletionRequestValidator _validator = new();

    [Fact]
    public void AnIssuedToken_IsValid()
    {
        Assert.True(_validator.Validate(new CancelDeletionRequest { Token = "abcDEF123_-xyz" }).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("<script>")]
    [InlineData("a/b+c=")]
    public void AMalformedToken_IsRefused_WithTheInvalidLinkMessage(string token)
    {
        ValidationResult result = _validator.Validate(new CancelDeletionRequest { Token = token });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == AccountDeletionMessages.InvalidCancelLink
                                            || e.PropertyName == nameof(CancelDeletionRequest.Token));
    }

    [Fact]
    public void ATooLongToken_IsRefused()
    {
        Assert.False(_validator.Validate(new CancelDeletionRequest { Token = new string('a', 101) }).IsValid);
    }
}
