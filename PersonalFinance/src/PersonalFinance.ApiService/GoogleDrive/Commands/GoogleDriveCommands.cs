using MediatR;
using PersonalFinance.ApiService.GoogleDrive.Services;
using PersonalFinance.Shared.DTOs.GoogleDrive;

namespace PersonalFinance.ApiService.GoogleDrive.Commands;

/// <summary>Command to connect a Google Drive folder.</summary>
public sealed class ConnectCommand : IRequest<GoogleDriveConnectionDto>
{
    public ConnectGoogleDriveRequestDto Request { get; set; } = null!;
}
public sealed class ConnectCommandHandler : IRequestHandler<ConnectCommand, GoogleDriveConnectionDto>
{
    private readonly IDriveConnectionService _service;
    public ConnectCommandHandler(IDriveConnectionService service) => _service = service;
    public async Task<GoogleDriveConnectionDto> Handle(ConnectCommand request, CancellationToken cancellationToken)
    {
        return await _service.ConnectDriveAsync(request.Request, cancellationToken);
    }
}

/// <summary>Command to sync a Google Drive connection.</summary>
public sealed class SyncCommand : IRequest<GoogleDriveFolderResponseDto>
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool ForceRefresh { get; set; }
}
public sealed class SyncCommandHandler : IRequestHandler<SyncCommand, GoogleDriveFolderResponseDto>
{
    private readonly IDriveConnectionService _service;
    public SyncCommandHandler(IDriveConnectionService service) => _service = service;
    public async Task<GoogleDriveFolderResponseDto> Handle(SyncCommand request, CancellationToken cancellationToken)
    {
        return await _service.SyncConnectionAsync(request.ConnectionId, request.UserId, request.ForceRefresh, cancellationToken);
    }
}

/// <summary>Command to re-authenticate a Google Drive connection.</summary>
public sealed class ReauthCommand : IRequest<GoogleDriveConnectionDto>
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
public sealed class ReauthCommandHandler : IRequestHandler<ReauthCommand, GoogleDriveConnectionDto>
{
    private readonly IDriveConnectionService _service;
    public ReauthCommandHandler(IDriveConnectionService service) => _service = service;
    public async Task<GoogleDriveConnectionDto> Handle(ReauthCommand request, CancellationToken cancellationToken)
    {
        return await _service.ReauthConnectionAsync(request.ConnectionId, request.UserId, request.ApiKey, cancellationToken);
    }
}

/// <summary>Command to delete a Google Drive connection.</summary>
public sealed class DeleteConnectionCommand : IRequest<bool>
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
public sealed class DeleteConnectionCommandHandler : IRequestHandler<DeleteConnectionCommand, bool>
{
    private readonly IDriveConnectionService _service;
    public DeleteConnectionCommandHandler(IDriveConnectionService service) => _service = service;
    public async Task<bool> Handle(DeleteConnectionCommand request, CancellationToken cancellationToken)
    {
        return await _service.DeleteConnectionAsync(request.ConnectionId, request.UserId, cancellationToken);
    }
}
