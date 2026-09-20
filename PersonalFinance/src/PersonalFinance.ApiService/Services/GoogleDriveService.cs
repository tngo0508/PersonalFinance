using System.Net.Http.Json;
using System.Text.Json;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation interacting with Google Drive API v3 to retrieve files within a folder,
/// with fallback preview support when no API key is supplied.
/// </summary>
public class GoogleDriveService : IGoogleDriveService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoogleDriveService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleDriveFolderResponseDto> GetFolderFilesAsync(
        GoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var rawInput = request.FolderUrl;
        var folderId = GoogleDriveHelper.ExtractFolderId(rawInput);

        if (string.IsNullOrWhiteSpace(folderId))
        {
            _logger.LogWarning("Invalid Google Drive folder link or ID provided: '{Input}'", rawInput);
            return new GoogleDriveFolderResponseDto
            {
                Success = false,
                FolderUrl = rawInput,
                ErrorMessage = "Invalid Google Drive folder URL or Folder ID. Please verify your link."
            };
        }

        var apiKey = !string.IsNullOrWhiteSpace(request.ApiKey)
            ? request.ApiKey.Trim()
            : _configuration["GoogleDrive:ApiKey"]?.Trim();

        // If an API key is available, attempt live fetch via Google Drive API v3
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                _logger.LogInformation("Querying Google Drive API v3 for folder ID '{FolderId}'...", folderId);
                return await FetchLiveFolderFilesAsync(folderId, rawInput, apiKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Google Drive explore request was canceled for folder '{FolderId}'.", folderId);
                return new GoogleDriveFolderResponseDto
                {
                    Success = false,
                    FolderId = folderId,
                    FolderUrl = rawInput,
                    ErrorMessage = "The request was canceled."
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Google Drive API request timed out for folder '{FolderId}'. Falling back to preview mode.", folderId);
                return GeneratePreviewResponse(
                    folderId,
                    rawInput,
                    "The request to Google Drive API timed out. Verify your network connectivity and Google Cloud status. Showing preview demo data.");
            }
            catch (TimeoutException timeoutEx)
            {
                _logger.LogWarning(timeoutEx, "Google Drive API request timed out for folder '{FolderId}'. Falling back to preview mode.", folderId);
                return GeneratePreviewResponse(
                    folderId,
                    rawInput,
                    "The request to Google Drive API timed out. Verify your network connectivity and Google Cloud status. Showing preview demo data.");
            }
            catch (HttpRequestException httpEx)
            {
                var statusCode = httpEx.StatusCode.HasValue ? (int)httpEx.StatusCode.Value : 0;
                string detailedGuidance;

                if (statusCode == 404)
                {
                    detailedGuidance = "Google Drive API returned HTTP 404 Not Found. This occurs when the folder ID is incorrect OR when folder permissions are set to 'Restricted' (Google masks private folders as 404). To resolve: open your folder in Google Drive, click 'Share', and change General access from 'Restricted' to 'Anyone with the link' (Viewer). Showing preview demo data.";
                }
                else if (statusCode == 403)
                {
                    detailedGuidance = "Google Drive API returned HTTP 403 Forbidden. This occurs when 'Google Drive API' is not enabled in your Google Cloud project, API key restrictions prevent access, or folder access is denied. To resolve: enable the Google Drive API in Google Cloud Console and ensure folder sharing is set to 'Anyone with the link'. Showing preview demo data.";
                }
                else if (statusCode == 400)
                {
                    detailedGuidance = "Google Drive API returned HTTP 400 Bad Request. Please verify that your API key is valid and formatted correctly. Showing preview demo data.";
                }
                else
                {
                    var statusCodeInfo = httpEx.StatusCode.HasValue
                        ? $"HTTP {(int)httpEx.StatusCode.Value} ({httpEx.StatusCode})"
                        : "an HTTP error";
                    detailedGuidance = $"Google Drive API returned {statusCodeInfo} (Verify that your API key is valid, Google Drive API is enabled, and folder access is set to 'Anyone with the link'). Showing preview demo data.";
                }

                _logger.LogWarning(httpEx, "Google Drive API request failed with HTTP {StatusCode}. Falling back to preview mode.", statusCode);
                return GeneratePreviewResponse(
                    folderId,
                    rawInput,
                    detailedGuidance);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Google Drive API returned unexpected response format for folder '{FolderId}'. Falling back to preview mode.", folderId);
                return GeneratePreviewResponse(
                    folderId,
                    rawInput,
                    "Google Drive API returned an unexpected response format. Showing preview demo data.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching from Google Drive API for folder '{FolderId}'.", folderId);
                return GeneratePreviewResponse(
                    folderId,
                    rawInput,
                    "An error occurred while connecting to Google Drive API. Showing preview demo data.");
            }
        }

        // When no API key is configured, return demo/sample file list with informative guidance
        _logger.LogInformation("No Google Drive API key provided. Returning preview data for folder '{FolderId}'.", folderId);
        return GeneratePreviewResponse(
            folderId,
            rawInput,
            "No Google Drive API Key was provided. Showing preview files for this folder. To query live Google Drive folders, provide a Google Cloud API Key with Google Drive API enabled.");
    }

    private async Task<GoogleDriveFolderResponseDto> FetchLiveFolderFilesAsync(
        string folderId,
        string rawInput,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var files = new List<GoogleDriveFileDto>();
        string? pageToken = null;
        string? folderName = null;

        // 1. Fetch Folder Metadata (Name)
        try
        {
            var folderMetaUrl = $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(folderId)}?fields=id,name,mimeType&key={Uri.EscapeDataString(apiKey)}";
            using var metaResponse = await _httpClient.GetAsync(folderMetaUrl, cancellationToken);
            if (metaResponse.IsSuccessStatusCode)
            {
                using var metaDoc = await JsonDocument.ParseAsync(await metaResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (metaDoc.RootElement.TryGetProperty("name", out var nameProp))
                {
                    folderName = nameProp.GetString();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch folder metadata for {FolderId}", folderId);
        }

        // 2. Fetch Files inside folder (with pagination)
        do
        {
            var query = $"'{folderId}' in parents and trashed = false";
            var fields = "nextPageToken,files(id,name,mimeType,size,createdTime,modifiedTime,webViewLink,iconLink,thumbnailLink)";
            var listUrl = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields={Uri.EscapeDataString(fields)}&orderBy={Uri.EscapeDataString("modifiedTime desc")}&pageSize=100&key={Uri.EscapeDataString(apiKey)}";

            if (!string.IsNullOrEmpty(pageToken))
            {
                listUrl += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            using var response = await _httpClient.GetAsync(listUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Google Drive API error: {Status} - {Error}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Google Drive API returned {(int)response.StatusCode}: {response.ReasonPhrase}", null, response.StatusCode);
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (root.TryGetProperty("files", out var filesArray) && filesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElem in filesArray.EnumerateArray())
                {
                    var id = fileElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                    var name = fileElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Untitled" : "Untitled";
                    var mimeType = fileElem.TryGetProperty("mimeType", out var mimeProp) ? mimeProp.GetString() ?? string.Empty : string.Empty;
                    
                    long? size = null;
                    if (fileElem.TryGetProperty("size", out var sizeProp) && long.TryParse(sizeProp.GetString(), out var parsedSize))
                    {
                        size = parsedSize;
                    }

                    DateTime? createdTime = null;
                    if (fileElem.TryGetProperty("createdTime", out var createdProp) && DateTime.TryParse(createdProp.GetString(), out var parsedCreated))
                    {
                        createdTime = parsedCreated;
                    }

                    DateTime? modifiedTime = null;
                    if (fileElem.TryGetProperty("modifiedTime", out var modifiedProp) && DateTime.TryParse(modifiedProp.GetString(), out var parsedModified))
                    {
                        modifiedTime = parsedModified;
                    }

                    var webViewLink = fileElem.TryGetProperty("webViewLink", out var webViewProp) ? webViewProp.GetString() : $"https://drive.google.com/file/d/{id}/view";
                    var iconLink = fileElem.TryGetProperty("iconLink", out var iconProp) ? iconProp.GetString() : null;
                    var thumbnailLink = fileElem.TryGetProperty("thumbnailLink", out var thumbProp) ? thumbProp.GetString() : null;

                    var typeInfo = GoogleDriveHelper.ResolveTypeInfo(mimeType, name);

                    files.Add(new GoogleDriveFileDto
                    {
                        Id = id,
                        Name = name,
                        MimeType = mimeType,
                        Size = size,
                        SizeFormatted = GoogleDriveHelper.FormatBytes(size),
                        CreatedTime = createdTime,
                        ModifiedTime = modifiedTime,
                        WebViewLink = webViewLink,
                        IconLink = iconLink,
                        ThumbnailLink = thumbnailLink,
                        FileType = typeInfo.FileType,
                        IconBadgeClass = typeInfo.BadgeClass,
                        IsFolder = typeInfo.IsFolder
                    });
                }
            }

            pageToken = root.TryGetProperty("nextPageToken", out var nextProp) ? nextProp.GetString() : null;

        } while (!string.IsNullOrEmpty(pageToken));

        var totalSizeBytes = files.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);
        string? emptyFolderWarning = null;
        if (files.Count == 0)
        {
            emptyFolderWarning = "No files found in this Google Drive folder. If the folder contains files, ensure that the folder's General access is set to 'Anyone with the link' (Viewer) in Google Drive.";
        }

        return new GoogleDriveFolderResponseDto
        {
            Success = true,
            FolderId = folderId,
            FolderUrl = rawInput,
            FolderName = folderName ?? $"Folder ({folderId})",
            TotalSizeBytes = totalSizeBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalSizeBytes),
            Files = files,
            WarningMessage = emptyFolderWarning,
            IsDemoData = false
        };
    }

    private static GoogleDriveFolderResponseDto GeneratePreviewResponse(string folderId, string rawInput, string warningMessage)
    {
        var sampleFiles = new List<GoogleDriveFileDto>
        {
            new()
            {
                Id = "1aBcD_Financial_Report_2026",
                Name = "Personal_Finance_Annual_Report_2026.pdf",
                MimeType = "application/pdf",
                Size = 4_250_000,
                SizeFormatted = "4.05 MB",
                CreatedTime = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 10, 14, 22, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/1aBcD_Financial_Report_2026/view",
                FileType = "PDF Document",
                IconBadgeClass = "bg-danger",
                IsFolder = false
            },
            new()
            {
                Id = "2eFgH_Monthly_Budget_Tracker",
                Name = "Monthly_Budget_and_Expense_Tracker.xlsx",
                MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Size = 1_820_000,
                SizeFormatted = "1.74 MB",
                CreatedTime = new DateTime(2026, 2, 1, 11, 0, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 18, 16, 45, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/2eFgH_Monthly_Budget_Tracker/view",
                FileType = "Spreadsheet",
                IconBadgeClass = "bg-success",
                IsFolder = false
            },
            new()
            {
                Id = "3iJkL_Investment_Portfolio_Overview",
                Name = "Investment_Portfolio_Q3_Overview.docx",
                MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                Size = 850_000,
                SizeFormatted = "830.08 KB",
                CreatedTime = new DateTime(2026, 7, 5, 8, 15, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 19, 10, 5, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/3iJkL_Investment_Portfolio_Overview/view",
                FileType = "Document",
                IconBadgeClass = "bg-primary",
                IsFolder = false
            },
            new()
            {
                Id = "4mNoP_Receipts_And_Invoices_Folder",
                Name = "Receipts & Invoices 2026",
                MimeType = "application/vnd.google-apps.folder",
                Size = null,
                SizeFormatted = "-",
                CreatedTime = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/drive/folders/4mNoP_Receipts_And_Invoices_Folder",
                FileType = "Folder",
                IconBadgeClass = "bg-primary",
                IsFolder = true
            },
            new()
            {
                Id = "5qRsT_Property_Deed_Scans",
                Name = "Property_Deed_Scan_HighRes.png",
                MimeType = "image/png",
                Size = 6_300_000,
                SizeFormatted = "6.01 MB",
                CreatedTime = new DateTime(2026, 4, 12, 14, 0, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 4, 12, 14, 0, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/5qRsT_Property_Deed_Scans/view",
                FileType = "Image",
                IconBadgeClass = "bg-info text-dark",
                IsFolder = false
            },
            new()
            {
                Id = "6uVwX_Tax_Return_Statement_2025",
                Name = "Tax_Return_Statement_Official.pdf",
                MimeType = "application/pdf",
                Size = 2_150_000,
                SizeFormatted = "2.05 MB",
                CreatedTime = new DateTime(2026, 3, 20, 10, 30, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 3, 22, 11, 0, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/6uVwX_Tax_Return_Statement_2025/view",
                FileType = "PDF Document",
                IconBadgeClass = "bg-danger",
                IsFolder = false
            },
            new()
            {
                Id = "7yZaB_Savings_Growth_Projection",
                Name = "Savings_Growth_Projection_Model.csv",
                MimeType = "text/csv",
                Size = 145_000,
                SizeFormatted = "141.6 KB",
                CreatedTime = new DateTime(2026, 5, 8, 17, 20, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 8, 30, 9, 10, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/7yZaB_Savings_Growth_Projection/view",
                FileType = "Spreadsheet",
                IconBadgeClass = "bg-success",
                IsFolder = false
            },
            new()
            {
                Id = "8cDeF_Retirement_Planning_Presentation",
                Name = "Retirement_Planning_Deck_2026.pptx",
                MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                Size = 12_400_000,
                SizeFormatted = "11.83 MB",
                CreatedTime = new DateTime(2026, 6, 14, 13, 0, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 15, 15, 30, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/8cDeF_Retirement_Planning_Presentation/view",
                FileType = "Presentation",
                IconBadgeClass = "bg-warning text-dark",
                IsFolder = false
            },
            new()
            {
                Id = "9gHiJ_Bank_Statements_Archive",
                Name = "Bank_Statements_Archive_2025_2026.zip",
                MimeType = "application/zip",
                Size = 25_800_000,
                SizeFormatted = "24.6 MB",
                CreatedTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                ModifiedTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                WebViewLink = $"https://drive.google.com/file/d/9gHiJ_Bank_Statements_Archive/view",
                FileType = "Archive",
                IconBadgeClass = "bg-secondary",
                IsFolder = false
            }
        };

        var totalSizeBytes = sampleFiles.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);

        return new GoogleDriveFolderResponseDto
        {
            Success = true,
            FolderId = folderId,
            FolderUrl = rawInput,
            FolderName = $"Folder ({folderId})",
            TotalSizeBytes = totalSizeBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalSizeBytes),
            Files = sampleFiles,
            WarningMessage = warningMessage,
            IsDemoData = true
        };
    }
}
