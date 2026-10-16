using MediatR;
using PersonalFinance.ApiService.AiAssistant.Services;
using PersonalFinance.Shared.DTOs.News;

namespace PersonalFinance.ApiService.AiAssistant.Commands;

/// <summary>
/// Command to summarize a news article using the AI assistant.
/// </summary>
public sealed class SummarizeArticleCommand : IRequest<NewsArticleSummaryResponseDto>
{
    public string UserId { get; set; } = string.Empty;
    public NewsArticleSummaryRequestDto Request { get; set; } = null!;
}

/// <summary>
/// Handler for SummarizeArticleCommand.
/// </summary>
public sealed class SummarizeArticleCommandHandler : IRequestHandler<SummarizeArticleCommand, NewsArticleSummaryResponseDto>
{
    private readonly IAiAssistantService _aiAssistantService;

    public SummarizeArticleCommandHandler(IAiAssistantService aiAssistantService)
    {
        _aiAssistantService = aiAssistantService;
    }

    public async Task<NewsArticleSummaryResponseDto> Handle(SummarizeArticleCommand request, CancellationToken cancellationToken)
    {
        return await _aiAssistantService.SummarizeArticleAsync(request.UserId, request.Request, cancellationToken);
    }
}
