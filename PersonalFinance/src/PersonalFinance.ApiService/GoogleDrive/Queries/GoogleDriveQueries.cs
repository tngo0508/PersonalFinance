using MediatR;
using PersonalFinance.ApiService.GoogleDrive.Services;
using PersonalFinance.ApiService.SpreadsheetAnalysis.Services;
using PersonalFinance.Shared.DTOs.GoogleDrive;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.GoogleDrive.Queries;

/// <summary>Query to get user's Google Drive connections.</summary>
public sealed class GetConnectionsQuery : IRequest<List<GoogleDriveConnectionDto>>
{
    public string UserId { get; set; } = string.Empty;
}
public sealed class GetConnectionsQueryHandler : IRequestHandler<GetConnectionsQuery, List<GoogleDriveConnectionDto>>
{
    private readonly IDriveConnectionService _service;
    public GetConnectionsQueryHandler(IDriveConnectionService service) => _service = service;
    public async Task<List<GoogleDriveConnectionDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetUserConnectionsAsync(request.UserId, cancellationToken);
    }
}

/// <summary>Query to get files for a specific connection.</summary>
public sealed class GetConnectionFilesQuery : IRequest<GoogleDriveFolderResponseDto>
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool ForceRefresh { get; set; }
}
public sealed class GetConnectionFilesQueryHandler : IRequestHandler<GetConnectionFilesQuery, GoogleDriveFolderResponseDto>
{
    private readonly IDriveConnectionService _service;
    public GetConnectionFilesQueryHandler(IDriveConnectionService service) => _service = service;
    public async Task<GoogleDriveFolderResponseDto> Handle(GetConnectionFilesQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetConnectionFilesAsync(request.ConnectionId, request.UserId, request.ForceRefresh, cancellationToken);
    }
}

/// <summary>Query to get spreadsheet budget report.</summary>
public sealed class GetSpreadsheetBudgetReportQuery : IRequest<MonthlyBudgetReportDto>
{
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public int? ConnectionId { get; set; }
    public string? UserId { get; set; }
}
public sealed class GetSpreadsheetBudgetReportQueryHandler : IRequestHandler<GetSpreadsheetBudgetReportQuery, MonthlyBudgetReportDto>
{
    private readonly ISpreadsheetReader _service;
    public GetSpreadsheetBudgetReportQueryHandler(ISpreadsheetReader service) => _service = service;
    public async Task<MonthlyBudgetReportDto> Handle(GetSpreadsheetBudgetReportQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetSpreadsheetBudgetReportAsync(request.FileId!, request.FileName, request.ConnectionId, request.UserId, cancellationToken);
    }
}

/// <summary>Query to get spreadsheet transactions.</summary>
public sealed class GetSpreadsheetTransactionsQuery : IRequest<SpreadsheetTransactionsDto>
{
    public string FileId { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public int? ConnectionId { get; set; }
    public string? UserId { get; set; }
}
public sealed class GetSpreadsheetTransactionsQueryHandler : IRequestHandler<GetSpreadsheetTransactionsQuery, SpreadsheetTransactionsDto>
{
    private readonly ISpreadsheetReader _service;
    public GetSpreadsheetTransactionsQueryHandler(ISpreadsheetReader service) => _service = service;
    public async Task<SpreadsheetTransactionsDto> Handle(GetSpreadsheetTransactionsQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetSpreadsheetTransactionsAsync(request.FileId, request.FileName, request.ConnectionId, request.UserId, cancellationToken);
    }
}
