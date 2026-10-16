using MediatR;
using PersonalFinance.ApiService.NetWorth.Services;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.NetWorth.Queries;

/// <summary>
/// Query to load net worth projection parameters for a user.
/// </summary>
public sealed class LoadParametersQuery : IRequest<NetWorthParametersDto?>
{
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Handler for LoadParametersQuery.
/// </summary>
public sealed class LoadParametersQueryHandler : IRequestHandler<LoadParametersQuery, NetWorthParametersDto?>
{
    private readonly INetWorthParametersService _service;

    public LoadParametersQueryHandler(INetWorthParametersService service)
    {
        _service = service;
    }

    public async Task<NetWorthParametersDto?> Handle(LoadParametersQuery request, CancellationToken cancellationToken)
    {
        return await _service.LoadAsync(request.UserId, cancellationToken);
    }
}
