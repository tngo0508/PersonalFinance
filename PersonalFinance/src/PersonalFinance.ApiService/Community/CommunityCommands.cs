using MediatR;
using PersonalFinance.ApiService.Community.Services;
using PersonalFinance.Shared.DTOs.Community;

namespace PersonalFinance.ApiService.Community;

/// <summary>
/// Query to get full community data.
/// </summary>
public sealed class GetCommunityQuery : IRequest<CommunityResponseDto>
{
    public string UserId { get; set; } = string.Empty;
}

public sealed class GetCommunityQueryHandler : IRequestHandler<GetCommunityQuery, CommunityResponseDto>
{
    private readonly ICommunityService _service;

    public GetCommunityQueryHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommunityResponseDto> Handle(GetCommunityQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetCommunityAsync(request.UserId, cancellationToken);
    }
}

/// <summary>
/// Command to vote on a poll.
/// </summary>
public sealed class VoteOnPollCommand : IRequest<CommunityVoteResultDto>
{
    public string UserId { get; set; } = string.Empty;
    public CommunityVoteRequestDto Request { get; set; } = new();
}

public sealed class VoteOnPollCommandHandler : IRequestHandler<VoteOnPollCommand, CommunityVoteResultDto>
{
    private readonly ICommunityService _service;

    public VoteOnPollCommandHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommunityVoteResultDto> Handle(VoteOnPollCommand request, CancellationToken cancellationToken)
    {
        return await _service.VoteAsync(request.UserId, request.Request, cancellationToken);
    }
}

/// <summary>
/// Command to add a comment.
/// </summary>
public sealed class AddCommentCommand : IRequest<CommunityCommentDto>
{
    public string UserId { get; set; } = string.Empty;
    public CommunityCommentRequestDto Request { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
}

public sealed class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, CommunityCommentDto>
{
    private readonly ICommunityService _service;

    public AddCommentCommandHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommunityCommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        return await _service.AddCommentAsync(request.UserId, request.Request, request.AuthorName, cancellationToken);
    }
}

/// <summary>
/// Command to toggle a comment like.
/// </summary>
public sealed class ToggleCommentLikeCommand : IRequest<CommentLikeResult>
{
    public string UserId { get; set; } = string.Empty;
    public int CommentId { get; set; }
}

public sealed class ToggleCommentLikeCommandHandler : IRequestHandler<ToggleCommentLikeCommand, CommentLikeResult>
{
    private readonly ICommunityService _service;

    public ToggleCommentLikeCommandHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommentLikeResult> Handle(ToggleCommentLikeCommand request, CancellationToken cancellationToken)
    {
        return await _service.ToggleCommentLikeAsync(request.UserId, request.CommentId, cancellationToken);
    }
}

/// <summary>
/// Query to get poll comments.
/// </summary>
public sealed class GetPollCommentsQuery : IRequest<List<CommunityCommentDto>>
{
    public string PollCode { get; set; } = string.Empty;
}

public sealed class GetPollCommentsQueryHandler : IRequestHandler<GetPollCommentsQuery, List<CommunityCommentDto>>
{
    private readonly ICommunityService _service;

    public GetPollCommentsQueryHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<List<CommunityCommentDto>> Handle(GetPollCommentsQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetPollCommentsAsync(request.PollCode, cancellationToken);
    }
}

/// <summary>
/// Command to submit a story.
/// </summary>
public sealed class SubmitStoryCommand : IRequest<CommunityStoryDto>
{
    public string UserId { get; set; } = string.Empty;
    public CommunityStoryRequestDto Request { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
}

public sealed class SubmitStoryCommandHandler : IRequestHandler<SubmitStoryCommand, CommunityStoryDto>
{
    private readonly ICommunityService _service;

    public SubmitStoryCommandHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommunityStoryDto> Handle(SubmitStoryCommand request, CancellationToken cancellationToken)
    {
        return await _service.SubmitStoryAsync(request.UserId, request.Request, request.AuthorName, cancellationToken);
    }
}

/// <summary>
/// Command to toggle a story like.
/// </summary>
public sealed class ToggleStoryLikeCommand : IRequest<StoryLikeResult>
{
    public string UserId { get; set; } = string.Empty;
    public int StoryId { get; set; }
}

public sealed class ToggleStoryLikeCommandHandler : IRequestHandler<ToggleStoryLikeCommand, StoryLikeResult>
{
    private readonly ICommunityService _service;

    public ToggleStoryLikeCommandHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<StoryLikeResult> Handle(ToggleStoryLikeCommand request, CancellationToken cancellationToken)
    {
        return await _service.ToggleStoryLikeAsync(request.UserId, request.StoryId, cancellationToken);
    }
}

/// <summary>
/// Query to get a story by ID.
/// </summary>
public sealed class GetStoryByIdQuery : IRequest<CommunityStoryDto?>
{
    public string UserId { get; set; } = string.Empty;
    public int StoryId { get; set; }
}

public sealed class GetStoryByIdQueryHandler : IRequestHandler<GetStoryByIdQuery, CommunityStoryDto?>
{
    private readonly ICommunityService _service;

    public GetStoryByIdQueryHandler(ICommunityService service)
    {
        _service = service;
    }

    public async Task<CommunityStoryDto?> Handle(GetStoryByIdQuery request, CancellationToken cancellationToken)
    {
        return await _service.GetStoryByIdAsync(request.UserId, request.StoryId, cancellationToken);
    }
}
