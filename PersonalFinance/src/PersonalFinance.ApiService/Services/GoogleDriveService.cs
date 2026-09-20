using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Data.Helpers;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Services;

/// <summary>
/// Service implementation interacting with Google Drive API v3 to retrieve files within a folder,
/// with SQLite caching, incremental synchronization, multi-drive connection persistence,
/// and re-authentication handling.
/// </summary>
public class GoogleDriveService : IGoogleDriveService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(
        HttpClient httpClient,
        IConfiguration configuration,
        AppDbContext dbContext,
        ILogger<GoogleDriveService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<List<GoogleDriveConnectionDto>> GetUserConnectionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<GoogleDriveConnectionDto>();
        }

        var connections = await _dbContext.GoogleDriveConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Include(c => c.CachedFiles)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return connections.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<GoogleDriveFolderResponseDto> GetConnectionFilesAsync(
        int connectionId,
        string userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.GoogleDriveConnections
            .Include(c => c.CachedFiles)
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

        if (connection == null)
        {
            return new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = "Google Drive connection not found."
            };
        }

        // If not forced and cache exists and is fresh (within 30 minutes), return cached data from SQLite
        var isCacheFresh = connection.LastSyncedAtUtc.HasValue &&
                           (DateTime.UtcNow - connection.LastSyncedAtUtc.Value) < TimeSpan.FromMinutes(30) &&
                           connection.CachedFiles.Any(f => !f.IsTrashed);

        if (!forceRefresh && isCacheFresh)
        {
            _logger.LogInformation("Serving Google Drive data from SQLite cache for connection {ConnectionId} ('{FolderId}')", connectionId, connection.FolderId);
            return MapConnectionToFolderResponse(connection);
        }

        // Otherwise synchronize with Google Drive
        _logger.LogInformation("Synchronizing Google Drive data for connection {ConnectionId} ('{FolderId}')...", connectionId, connection.FolderId);
        return await SyncConnectionInternalAsync(connection, forceRefresh, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GoogleDriveConnectionDto> ConnectDriveAsync(
        ConnectGoogleDriveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("UserId is required to connect a Google Drive.", nameof(request));
        }

        var folderId = GoogleDriveHelper.ExtractFolderId(request.FolderUrl);
        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw new ArgumentException("Invalid Google Drive folder URL or Folder ID.", nameof(request));
        }

        // Check if connection for this user and folder already exists
        var existing = await _dbContext.GoogleDriveConnections
            .Include(c => c.CachedFiles)
            .FirstOrDefaultAsync(c => c.UserId == request.UserId && c.FolderId == folderId, cancellationToken);

        GoogleDriveConnection connection;

        if (existing != null)
        {
            connection = existing;
            if (!string.IsNullOrWhiteSpace(request.ConnectionName))
            {
                connection.Name = request.ConnectionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                connection.EncryptedApiKey = CredentialProtector.Encrypt(request.ApiKey);
                connection.MaskedApiKey = CredentialProtector.Mask(request.ApiKey);
            }

            connection.IsValid = true;
            connection.SyncStatus = "Pending";
            connection.ErrorMessage = null;
        }
        else
        {
            connection = new GoogleDriveConnection
            {
                UserId = request.UserId,
                FolderId = folderId,
                FolderUrl = request.FolderUrl,
                Name = !string.IsNullOrWhiteSpace(request.ConnectionName) ? request.ConnectionName.Trim() : "Google Drive Folder",
                EncryptedApiKey = CredentialProtector.Encrypt(request.ApiKey),
                MaskedApiKey = CredentialProtector.Mask(request.ApiKey),
                IsValid = true,
                SyncStatus = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.GoogleDriveConnections.Add(connection);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Perform initial synchronization & SQLite caching
        await SyncConnectionInternalAsync(connection, forceRefresh: true, cancellationToken);

        return MapToDto(connection);
    }

    /// <inheritdoc />
    public async Task<GoogleDriveFolderResponseDto> SyncConnectionAsync(
        int connectionId,
        string userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.GoogleDriveConnections
            .Include(c => c.CachedFiles)
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

        if (connection == null)
        {
            return new GoogleDriveFolderResponseDto
            {
                Success = false,
                ErrorMessage = "Google Drive connection not found."
            };
        }

        return await SyncConnectionInternalAsync(connection, forceRefresh, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GoogleDriveConnectionDto> ReauthConnectionAsync(
        int connectionId,
        string userId,
        string? newApiKey,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.GoogleDriveConnections
            .Include(c => c.CachedFiles)
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

        if (connection == null)
        {
            throw new KeyNotFoundException($"Connection with ID {connectionId} not found for user.");
        }

        if (!string.IsNullOrWhiteSpace(newApiKey))
        {
            connection.EncryptedApiKey = CredentialProtector.Encrypt(newApiKey);
            connection.MaskedApiKey = CredentialProtector.Mask(newApiKey);
        }

        connection.IsValid = true;
        connection.SyncStatus = "Pending";
        connection.ErrorMessage = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Sync with new credentials
        await SyncConnectionInternalAsync(connection, forceRefresh: true, cancellationToken);

        return MapToDto(connection);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConnectionAsync(
        int connectionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.GoogleDriveConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, cancellationToken);

        if (connection == null)
        {
            return false;
        }

        _dbContext.GoogleDriveConnections.Remove(connection);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted Google Drive connection {ConnectionId} and purged its SQLite cache.", connectionId);
        return true;
    }

    private async Task<GoogleDriveFolderResponseDto> SyncConnectionInternalAsync(
        GoogleDriveConnection connection,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var apiKey = CredentialProtector.Decrypt(connection.EncryptedApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = _configuration["GoogleDrive:ApiKey"]?.Trim();
        }

        GoogleDriveFolderResponseDto liveResponse;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                liveResponse = await FetchLiveFolderFilesAsync(connection.FolderId, connection.FolderUrl, apiKey, cancellationToken);
            }
            catch (HttpRequestException httpEx) when (httpEx.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning(httpEx, "Google Drive API returned credential error HTTP {StatusCode} for connection {ConnectionId}.", httpEx.StatusCode, connection.Id);
                
                connection.IsValid = false;
                connection.SyncStatus = "NeedsReauth";
                connection.ErrorMessage = "Google credentials are invalid or expired. Please re-authenticate or update your API key.";
                await _dbContext.SaveChangesAsync(cancellationToken);

                var cachedFallback = MapConnectionToFolderResponse(connection);
                cachedFallback.Success = false;
                cachedFallback.ErrorMessage = connection.ErrorMessage;
                return cachedFallback;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error synchronizing live files from Google Drive API for connection {ConnectionId}. Falling back to preview/cache.", connection.Id);
                liveResponse = GeneratePreviewResponse(
                    connection.FolderId,
                    connection.FolderUrl,
                    "Unable to reach Google Drive API. Showing preview/cached data.");
            }
        }
        else
        {
            liveResponse = GeneratePreviewResponse(
                connection.FolderId,
                connection.FolderUrl,
                "No Google Drive API Key was provided. Showing preview files for this folder.");
        }

        if (liveResponse.Success)
        {
            if (!string.IsNullOrWhiteSpace(liveResponse.FolderName) && (connection.Name == "Google Drive" || connection.Name == "Google Drive Folder"))
            {
                connection.Name = liveResponse.FolderName;
            }

            // Incremental cache update
            var existingFilesMap = connection.CachedFiles.ToDictionary(f => f.DriveFileId, f => f);
            var activeFileIds = new HashSet<string>();

            foreach (var fetchedFile in liveResponse.Files)
            {
                activeFileIds.Add(fetchedFile.Id);

                if (existingFilesMap.TryGetValue(fetchedFile.Id, out var existingFile))
                {
                    // Update existing cached item if changed
                    existingFile.Name = fetchedFile.Name;
                    existingFile.MimeType = fetchedFile.MimeType;
                    existingFile.Size = fetchedFile.Size;
                    existingFile.SizeFormatted = fetchedFile.SizeFormatted;
                    existingFile.CreatedTime = fetchedFile.CreatedTime;
                    existingFile.ModifiedTime = fetchedFile.ModifiedTime;
                    existingFile.WebViewLink = fetchedFile.WebViewLink;
                    existingFile.IconLink = fetchedFile.IconLink;
                    existingFile.ThumbnailLink = fetchedFile.ThumbnailLink;
                    existingFile.FileType = fetchedFile.FileType;
                    existingFile.IconBadgeClass = fetchedFile.IconBadgeClass;
                    existingFile.IsFolder = fetchedFile.IsFolder;
                    existingFile.IsTrashed = false;
                    existingFile.LastFetchedUtc = DateTime.UtcNow;
                }
                else
                {
                    // Add new cached item
                    var newCachedFile = new GoogleDriveCachedFile
                    {
                        ConnectionId = connection.Id,
                        DriveFileId = fetchedFile.Id,
                        Name = fetchedFile.Name,
                        MimeType = fetchedFile.MimeType,
                        Size = fetchedFile.Size,
                        SizeFormatted = fetchedFile.SizeFormatted,
                        CreatedTime = fetchedFile.CreatedTime,
                        ModifiedTime = fetchedFile.ModifiedTime,
                        WebViewLink = fetchedFile.WebViewLink,
                        IconLink = fetchedFile.IconLink,
                        ThumbnailLink = fetchedFile.ThumbnailLink,
                        FileType = fetchedFile.FileType,
                        IconBadgeClass = fetchedFile.IconBadgeClass,
                        IsFolder = fetchedFile.IsFolder,
                        IsTrashed = false,
                        LastFetchedUtc = DateTime.UtcNow
                    };

                    connection.CachedFiles.Add(newCachedFile);
                }
            }

            // Purge cached files that no longer exist in Drive
            var filesToRemove = connection.CachedFiles
                .Where(f => !activeFileIds.Contains(f.DriveFileId))
                .ToList();

            foreach (var removedFile in filesToRemove)
            {
                _dbContext.GoogleDriveCachedFiles.Remove(removedFile);
            }

            connection.LastSyncedAtUtc = DateTime.UtcNow;
            connection.SyncStatus = "Synced";
            connection.IsValid = true;
            connection.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapConnectionToFolderResponse(connection, liveResponse.WarningMessage, liveResponse.IsDemoData);
    }

    private static GoogleDriveConnectionDto MapToDto(GoogleDriveConnection connection)
    {
        var activeFiles = connection.CachedFiles.Where(f => !f.IsTrashed).ToList();
        var totalBytes = activeFiles.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);

        return new GoogleDriveConnectionDto
        {
            Id = connection.Id,
            UserId = connection.UserId,
            Name = connection.Name,
            FolderId = connection.FolderId,
            FolderUrl = connection.FolderUrl,
            MaskedApiKey = connection.MaskedApiKey,
            IsValid = connection.IsValid,
            SyncStatus = connection.SyncStatus,
            ErrorMessage = connection.ErrorMessage,
            CreatedAtUtc = connection.CreatedAtUtc,
            LastSyncedAtUtc = connection.LastSyncedAtUtc,
            CachedFilesCount = activeFiles.Count,
            TotalSizeBytes = totalBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalBytes)
        };
    }

    private static GoogleDriveFolderResponseDto MapConnectionToFolderResponse(
        GoogleDriveConnection connection,
        string? warningMessage = null,
        bool? isDemoData = null)
    {
        var activeFiles = connection.CachedFiles
            .Where(f => !f.IsTrashed)
            .OrderByDescending(f => f.IsFolder)
            .ThenByDescending(f => f.ModifiedTime)
            .Select(f => new GoogleDriveFileDto
            {
                Id = f.DriveFileId,
                Name = f.Name,
                MimeType = f.MimeType,
                Size = f.Size,
                SizeFormatted = f.SizeFormatted,
                CreatedTime = f.CreatedTime,
                ModifiedTime = f.ModifiedTime,
                WebViewLink = f.WebViewLink,
                IconLink = f.IconLink,
                ThumbnailLink = f.ThumbnailLink,
                FileType = f.FileType,
                IconBadgeClass = f.IconBadgeClass,
                IsFolder = f.IsFolder
            })
            .ToList();

        var totalBytes = activeFiles.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);

        return new GoogleDriveFolderResponseDto
        {
            Success = connection.IsValid,
            FolderId = connection.FolderId,
            FolderUrl = connection.FolderUrl,
            FolderName = connection.Name,
            Files = activeFiles,
            TotalSizeBytes = totalBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalBytes),
            ErrorMessage = connection.ErrorMessage,
            WarningMessage = warningMessage,
            IsDemoData = isDemoData ?? string.IsNullOrEmpty(connection.EncryptedApiKey)
        };
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
                    if (fileElem.TryGetProperty("modifiedTime", out var modProp) && DateTime.TryParse(modProp.GetString(), out var parsedMod))
                    {
                        modifiedTime = parsedMod;
                    }

                    var webViewLink = fileElem.TryGetProperty("webViewLink", out var webLinkProp) ? webLinkProp.GetString() : null;
                    var iconLink = fileElem.TryGetProperty("iconLink", out var iconProp) ? iconProp.GetString() : null;
                    var thumbnailLink = fileElem.TryGetProperty("thumbnailLink", out var thumbProp) ? thumbProp.GetString() : null;

                    var (fileType, badgeClass, isFolder) = GoogleDriveHelper.ResolveTypeInfo(mimeType, name);

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
                        FileType = fileType,
                        IconBadgeClass = badgeClass,
                        IsFolder = isFolder
                    });
                }
            }

            pageToken = root.TryGetProperty("nextPageToken", out var tokenProp) ? tokenProp.GetString() : null;
        } while (!string.IsNullOrEmpty(pageToken));

        var totalBytes = files.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);

        string? emptyWarning = null;
        if (files.Count == 0)
        {
            emptyWarning = "No files were returned by the Google Drive API for this folder. Verify that the folder contains files, that General access is set to 'Anyone with the link' (Viewer), and that files are not restricted.";
        }

        return new GoogleDriveFolderResponseDto
        {
            Success = true,
            FolderId = folderId,
            FolderUrl = rawInput,
            FolderName = folderName ?? "Google Drive Folder",
            Files = files,
            TotalSizeBytes = totalBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalBytes),
            IsDemoData = false,
            WarningMessage = emptyWarning
        };
    }

    private GoogleDriveFolderResponseDto GeneratePreviewResponse(
        string folderId,
        string rawInput,
        string warningMessage)
    {
        var sampleFiles = new List<GoogleDriveFileDto>
        {
            new()
            {
                Id = "sample-doc-1",
                Name = "Annual_Financial_Report_2025.pdf",
                MimeType = "application/pdf",
                Size = 2_450_000,
                SizeFormatted = GoogleDriveHelper.FormatBytes(2_450_000),
                CreatedTime = DateTime.UtcNow.AddDays(-14),
                ModifiedTime = DateTime.UtcNow.AddDays(-2),
                WebViewLink = $"https://drive.google.com/file/d/sample-doc-1/view",
                FileType = "PDF Document",
                IconBadgeClass = "bg-danger",
                IsFolder = false
            },
            new()
            {
                Id = "sample-sheet-1",
                Name = "Household_Budget_Model.xlsx",
                MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Size = 1_120_000,
                SizeFormatted = GoogleDriveHelper.FormatBytes(1_120_000),
                CreatedTime = DateTime.UtcNow.AddDays(-30),
                ModifiedTime = DateTime.UtcNow.AddHours(-18),
                WebViewLink = $"https://drive.google.com/file/d/sample-sheet-1/view",
                FileType = "Spreadsheet",
                IconBadgeClass = "bg-success",
                IsFolder = false
            },
            new()
            {
                Id = "sample-folder-1",
                Name = "Tax_Receipts_2025",
                MimeType = "application/vnd.google-apps.folder",
                Size = null,
                SizeFormatted = "-",
                CreatedTime = DateTime.UtcNow.AddMonths(-3),
                ModifiedTime = DateTime.UtcNow.AddDays(-5),
                WebViewLink = $"https://drive.google.com/drive/folders/sample-folder-1",
                FileType = "Folder",
                IconBadgeClass = "bg-warning text-dark",
                IsFolder = true
            },
            new()
            {
                Id = "sample-img-1",
                Name = "Investment_Portfolio_Q4_Chart.png",
                MimeType = "image/png",
                Size = 850_000,
                SizeFormatted = GoogleDriveHelper.FormatBytes(850_000),
                CreatedTime = DateTime.UtcNow.AddDays(-7),
                ModifiedTime = DateTime.UtcNow.AddDays(-1),
                WebViewLink = $"https://drive.google.com/file/d/sample-img-1/view",
                FileType = "Image",
                IconBadgeClass = "bg-info text-dark",
                IsFolder = false
            },
            new()
            {
                Id = "sample-doc-2",
                Name = "Retirement_Strategy_Notes.gdoc",
                MimeType = "application/vnd.google-apps.document",
                Size = null,
                SizeFormatted = "-",
                CreatedTime = DateTime.UtcNow.AddMonths(-1),
                ModifiedTime = DateTime.UtcNow.AddHours(-6),
                WebViewLink = $"https://drive.google.com/file/d/sample-doc-2/view",
                FileType = "Document",
                IconBadgeClass = "bg-primary",
                IsFolder = false
            }
        };

        var totalBytes = sampleFiles.Where(f => f.Size.HasValue).Sum(f => f.Size!.Value);

        return new GoogleDriveFolderResponseDto
        {
            Success = true,
            FolderId = folderId,
            FolderUrl = rawInput,
            FolderName = "Financial Documents (Preview)",
            Files = sampleFiles,
            TotalSizeBytes = totalBytes,
            TotalSizeFormatted = GoogleDriveHelper.FormatBytes(totalBytes),
            IsDemoData = true,
            WarningMessage = warningMessage
        };
    }
}
