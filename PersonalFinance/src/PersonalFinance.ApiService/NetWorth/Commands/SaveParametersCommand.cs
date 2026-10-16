using MediatR;
using PersonalFinance.ApiService.NetWorth.Services;
using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.NetWorth.Commands;

/// <summary>
/// Command to save net worth projection parameters for a user.
/// </summary>
public sealed class SaveParametersCommand : IRequest<NetWorthParametersDto>
{
    public string UserId { get; set; } = string.Empty;
    public NetWorthProjectionRequestDto Parameters { get; set; } = null!;
}

/// <summary>
/// Handler for SaveParametersCommand.
/// </summary>
public sealed class SaveParametersCommandHandler : IRequestHandler<SaveParametersCommand, NetWorthParametersDto>
{
    private readonly INetWorthParametersService _service;

    public SaveParametersCommandHandler(INetWorthParametersService service)
    {
        _service = service;
    }

    public async Task<NetWorthParametersDto> Handle(SaveParametersCommand request, CancellationToken cancellationToken)
    {
        return await _service.SaveAsync(request.UserId, request.Parameters, cancellationToken);
    }
}
