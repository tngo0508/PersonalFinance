using MediatR;
using PersonalFinance.ApiService.AiAssistant.Services;
using PersonalFinance.Shared.DTOs.AiAssistant;

namespace PersonalFinance.ApiService.AiAssistant.Commands;

/// <summary>
/// Command to send a chat message to the AI assistant.
/// </summary>
public sealed class ChatCommand : IRequest<AiChatResponseDto>
{
    public string UserId { get; set; } = string.Empty;
    public AiChatRequestDto Request { get; set; } = null!;
}

/// <summary>
/// Handler for ChatCommand.
/// </summary>
public sealed class ChatCommandHandler : IRequestHandler<ChatCommand, AiChatResponseDto>
{
    private readonly IAiAssistantService _aiAssistantService;

    public ChatCommandHandler(IAiAssistantService aiAssistantService)
    {
        _aiAssistantService = aiAssistantService;
    }

    public async Task<AiChatResponseDto> Handle(ChatCommand request, CancellationToken cancellationToken)
    {
        return await _aiAssistantService.SendChatMessageAsync(request.UserId, request.Request, cancellationToken);
    }
}
