using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.CurrencyConverter;

namespace PersonalFinance.ApiService.Features.CurrencyConverter;

/// <summary>
/// API controller for the Currency Converter. It reads only stored, cached exchange rates and never calls a rate
/// source per request, so it has no rate limit of its own.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CurrencyConverterController : ControllerBase
{
    private readonly ICurrencyConversionService _conversionService;
    private readonly IValidator<ConvertCurrencyRequestDto> _validator;

    public CurrencyConverterController(ICurrencyConversionService conversionService,
        IValidator<ConvertCurrencyRequestDto> validator)
    {
        _conversionService = conversionService;
        _validator = validator;
    }

    /// <summary>
    /// Converts an amount between two Convertible Currencies, at the latest rate or the rate of a past date.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CurrencyConversionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Convert([FromQuery] ConvertCurrencyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationResult validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToApiError());
        }

        CurrencyConversionDto result = await _conversionService.ConvertAsync(request, cancellationToken);
        return Ok(result);
    }
}
