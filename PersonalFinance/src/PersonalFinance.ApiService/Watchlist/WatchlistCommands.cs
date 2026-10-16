using MediatR;
using PersonalFinance.ApiService.Watchlist.Services;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Watchlist;

/// <summary>
/// Command to save an article to the watchlist.
/// </summary>
public sealed class SaveArticleCommand : IRequest<SaveArticleResultDto>
{
    public string UserId { get; set; } = string.Empty;
    public SaveArticleRequestDto Request { get; set; } = new();
}

public sealed class SaveArticleCommandHandler : IRequestHandler<SaveArticleCommand, SaveArticleResultDto>
{
    private readonly IWatchlistService _service;

    public SaveArticleCommandHandler(IWatchlistService service)
    {
        _service = service;
    }

    public async Task<SaveArticleResultDto> Handle(SaveArticleCommand request, CancellationToken cancellationToken)
    {
        return await _service.SaveArticleAsync(request.UserId, request.Request, cancellationToken);
    }
}

/// <summary>
/// Command to remove an article from the watchlist.
/// </summary>
public sealed class RemoveArticleCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public int Id { get; set; }
}

public sealed class RemoveArticleCommandHandler : IRequestHandler<RemoveArticleCommand, bool>
{
    private readonly IWatchlistService _service;

    public RemoveArticleCommandHandler(IWatchlistService service)
    {
        _service = service;
    }

    public async Task<bool> Handle(RemoveArticleCommand request, CancellationToken cancellationToken)
    {
        return await _service.RemoveArticleAsync(request.UserId, request.Id, cancellationToken);
    }
}

/// <summary>
/// Query to get all saved articles for a user.
/// </summary>
public sealed class GetWatchlistQuery : IRequest<List<SavedArticleResponseDto>>
{
    public string UserId { get; set; } = string.Empty;
}

public sealed class GetWatchlistQueryHandler : IRequestHandler<GetWatchlistQuery, List<SavedArticleResponseDto>>
{
    private readonly IWatchlistService _service;

    public GetWatchlistQueryHandler(IWatchlistService service)
    {
        _service = service;
    }

    public async Task<List<SavedArticleResponseDto>> Handle(GetWatchlistQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetWatchlistAsync(request.UserId, cancellationToken);
    }
}

/// <summary>
/// Query to check if an article is saved.
/// </summary>
public sealed class CheckArticleSavedQuery : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class CheckArticleSavedQueryHandler : IRequestHandler<CheckArticleSavedQuery, bool>
{
    private readonly IWatchlistService _service;

    public CheckArticleSavedQueryHandler(IWatchlistService service)
    {
        _service = service;
    }

    public async Task<bool> Handle(CheckArticleSavedQuery request, CancellationToken cancellationToken)
    {
        return await _service.IsArticleSavedAsync(request.UserId, request.Url, cancellationToken);
    }
}
