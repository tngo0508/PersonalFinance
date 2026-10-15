using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.ApiService.Features.SpreadsheetAnalysis;

/// <summary>
///     Command to sync budget data from a Google Drive spreadsheet to the database.
/// </summary>
public sealed class SyncSpreadsheetBudgetCommand : IRequest<SyncSpreadsheetBudgetResult>
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
}

/// <summary>
///     Result of a sync operation.
/// </summary>
public sealed class SyncSpreadsheetBudgetResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasConflict { get; set; }
    public List<MonthConflictInfo> Conflicts { get; set; } = new();
    public int SnapshotsCreated { get; set; }
    public int TransactionsCreated { get; set; }
}

/// <summary>
///     Information about a conflicting month during sync.
/// </summary>
public sealed class MonthConflictInfo
{
    public int SnapshotId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}

/// <summary>
///     Handler for SyncSpreadsheetBudgetCommand.
/// </summary>
public sealed class
    SyncSpreadsheetBudgetCommandHandler : IRequestHandler<SyncSpreadsheetBudgetCommand, SyncSpreadsheetBudgetResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IGoogleDriveService _googleDriveService;

    public SyncSpreadsheetBudgetCommandHandler(IGoogleDriveService googleDriveService, AppDbContext dbContext)
    {
        _googleDriveService = googleDriveService;
        _dbContext = dbContext;
    }

    public async Task<SyncSpreadsheetBudgetResult> Handle(SyncSpreadsheetBudgetCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify connection ownership
            var connection = await _dbContext.GoogleDriveConnections
                .FirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.UserId == request.UserId,
                    cancellationToken);

            if (connection == null)
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    ErrorMessage = "Google Drive connection not found or access denied."
                };

            // Parse the spreadsheet from Google Drive
            MonthlyBudgetReportDto budgetReport;
            try
            {
                budgetReport = await _googleDriveService.GetSpreadsheetBudgetReportAsync(
                    request.FileId,
                    request.FileName,
                    request.ConnectionId,
                    request.UserId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to read spreadsheet: {ex.Message}"
                };
            }

            // Check for conflicts
            var conflicts = new List<MonthConflictInfo>();
            foreach (var month in budgetReport.Months)
            {
                var existing = await _dbContext.SpreadsheetBudgetSnapshots
                    .AnyAsync(s => s.ConnectionId == request.ConnectionId
                                   && s.Year == budgetReport.Year
                                   && s.MonthNumber == month.MonthNumber, cancellationToken);

                if (existing)
                {
                    var existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
                        .FirstOrDefaultAsync(s => s.ConnectionId == request.ConnectionId
                                                  && s.Year == budgetReport.Year
                                                  && s.MonthNumber == month.MonthNumber, cancellationToken);

                    if (existingSnapshot != null)
                        conflicts.Add(new MonthConflictInfo
                        {
                            SnapshotId = existingSnapshot.Id,
                            MonthNumber = month.MonthNumber,
                            MonthName = month.MonthName,
                            Year = budgetReport.Year,
                            SyncedAtUtc = existingSnapshot.SyncedAtUtc
                        });
                }
            }

            if (conflicts.Any() && !request.Overwrite)
                return new SyncSpreadsheetBudgetResult
                {
                    Success = false,
                    HasConflict = true,
                    Conflicts = conflicts,
                    ErrorMessage = "Some months already exist in the database. Confirm overwrite to proceed."
                };

            // Remove existing data if overwriting
            if (request.Overwrite)
            {
                foreach (var conflict in conflicts)
                {
                    var existingSnapshot = await _dbContext.SpreadsheetBudgetSnapshots
                        .FirstOrDefaultAsync(s => s.Id == conflict.SnapshotId, cancellationToken);

                    if (existingSnapshot != null) _dbContext.SpreadsheetBudgetSnapshots.Remove(existingSnapshot);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Store budget snapshots
            var snapshotsCreated = 0;
            foreach (var month in budgetReport.Months)
            {
                var categoryJson = JsonSerializer.Serialize(
                    month.Categories.Select(c => new
                    {
                        c.CategoryName,
                        c.BudgetedAmount,
                        c.ActualAmount
                    }).ToList());

                var snapshot = new SpreadsheetBudgetSnapshot
                {
                    ConnectionId = request.ConnectionId,
                    UserId = request.UserId,
                    FileName = budgetReport.FileName,
                    FileId = request.FileId,
                    MonthNumber = month.MonthNumber,
                    MonthName = month.MonthName,
                    Year = budgetReport.Year,
                    BudgetedIncome = month.BudgetedIncome,
                    ActualIncome = month.ActualIncome,
                    BudgetedExpenses = month.BudgetedExpenses,
                    ActualExpenses = month.ActualExpenses,
                    StartingBalance = month.StartingBalance,
                    EndingBalance = month.EndingBalance,
                    CategoryBreakdownJson = categoryJson,
                    SyncedAtUtc = DateTime.UtcNow
                };

                _dbContext.SpreadsheetBudgetSnapshots.Add(snapshot);
                snapshotsCreated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Parse and store transactions
            SpreadsheetTransactionsDto transactionsDto;
            try
            {
                transactionsDto = await _googleDriveService.GetSpreadsheetTransactionsAsync(
                    request.FileId,
                    request.FileName,
                    request.ConnectionId,
                    request.UserId,
                    cancellationToken);
            }
            catch
            {
                // Transactions are optional, continue without them
                await _dbContext.SaveChangesAsync(cancellationToken);
                return new SyncSpreadsheetBudgetResult
                {
                    Success = true,
                    SnapshotsCreated = snapshotsCreated,
                    TransactionsCreated = 0
                };
            }

            var transactionsCreated = 0;
            var snapshots = await _dbContext.SpreadsheetBudgetSnapshots
                .Where(s => s.ConnectionId == request.ConnectionId
                            && s.FileId == request.FileId
                            && s.Year == budgetReport.Year)
                .ToDictionaryAsync(s => s.MonthNumber, cancellationToken);

            foreach (var transaction in transactionsDto.Transactions)
            {
                if (!transaction.Date.HasValue) continue;

                var date = transaction.Date.Value;
                var monthNumber = date.Month;

                if (!snapshots.TryGetValue(monthNumber, out var snapshot)) continue;

                var record = new SpreadsheetTransactionRecord
                {
                    SnapshotId = snapshot.Id,
                    ConnectionId = request.ConnectionId,
                    UserId = request.UserId,
                    Date = date,
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    Category = transaction.Category,
                    Type = transaction.Type
                };

                _dbContext.SpreadsheetTransactionRecords.Add(record);
                transactionsCreated++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new SyncSpreadsheetBudgetResult
            {
                Success = true,
                SnapshotsCreated = snapshotsCreated,
                TransactionsCreated = transactionsCreated
            };
        }
        catch (Exception ex)
        {
            return new SyncSpreadsheetBudgetResult
            {
                Success = false,
                ErrorMessage = $"Sync failed: {ex.Message}"
            };
        }
    }
}