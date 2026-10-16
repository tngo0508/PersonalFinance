using MediatR;
using PersonalFinance.ApiService.NetWorth.Services;

namespace PersonalFinance.ApiService.NetWorth.Commands;

/// <summary>
/// Command to delete net worth projection parameters for a user.
/// </summary>
public sealed class DeleteParametersCommand : IRequest<Unit>
{
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Handler for DeleteParametersCommand.
/// </summary>
public sealed class DeleteParametersCommandHandler : IRequestHandler<DeleteParametersCommand, Unit>
{
    private readonly INetWorthParametersService _service;

    public DeleteParametersCommandHandler(INetWorthParametersService service)
    {
        _service = service;
    }

    public async Task<Unit> Handle(DeleteParametersCommand request, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(request.UserId, cancellationToken);
        return Unit.Value;
    }
}
