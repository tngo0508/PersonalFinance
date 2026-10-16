using MediatR;
using PersonalFinance.ApiService.GoogleDrive.Services;
using PersonalFinance.Shared.DTOs.GoogleDrive;

namespace PersonalFinance.ApiService.GoogleDrive.Commands;

/// <summary>
/// Command to explore a Google Drive folder.
/// </summary>
public sealed class ExploreCommand : IRequest<GoogleDriveFolderResponseDto>
{
    public GoogleDriveRequestDto Request { get; set; } = null!;
}

public sealed class ExploreCommandHandler : IRequestHandler<ExploreCommand, GoogleDriveFolderResponseDto>
{
    private readonly IDriveConnectionService _service;

    public ExploreCommandHandler(IDriveConnectionService service) => _service = service;

    public async Task<GoogleDriveFolderResponseDto> Handle(ExploreCommand request, CancellationToken cancellationToken)
    {
        return await _service.GetFolderFilesAsync(request.Request, cancellationToken);
    }
}
