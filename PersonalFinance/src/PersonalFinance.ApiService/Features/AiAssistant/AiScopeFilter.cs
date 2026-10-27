using System.Text.RegularExpressions;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;

namespace PersonalFinance.ApiService.Features.AiAssistant;

/// <summary>
/// Deterministic classifier that checks whether a user message falls within the AI assistant's
/// personal finance scope. Uses keyword matching so it never calls the LLM for off-topic queries.
/// </summary>
internal sealed class AiScopeFilter : IAiScopeFilter
{
    /// <summary>
    /// Keywords that indicate the message is about personal finance.
    /// </summary>
    private static readonly string[] FinanceKeywords =
    [
        // Budgeting and spending
        "budget", "spend", "spending", "expense", "expenses", "cost", "costs", "price", "prices",
        "groceries", "grocery", "dining", "dining out", "restaurant", "rent", "mortgage",
        "utility", "utilities", "bill", "bills", "subscription", "subscriptions",
        // Savings and goals
        "savings", "saving", "save", "goal", "goals", "emergency fund", "emergency",
        "target", "target amount",
        // Debt
        "debt", "loan", "loans", "credit card", "interest", "payoff", "pay off",
        "installment", "principal",
        // Net worth and investments (general, not stock picking)
        "net worth", "asset", "assets", "liability", "liabilities", "portfolio",
        "retirement", "401k", "401(k)", "ira", "roth", "invest", "investing",
        "allocation", "asset allocation",
        // Income and career
        "income", "salary", "wage", "wages", "paycheck", "pay", "compensation",
        "bonus", "raise", "negotiation", "career",
        // Tax and insurance (general concepts)
        "tax", "taxes", "deduction", "deductions", "filing", "insurance",
        "premium", "coverage",
        // App navigation
        "dashboard", "report", "chart", "graph", "settings",
        // General finance terms
        "finance", "financial", "money", "cash", "bank", "account", "accounts",
        "overdraft", "fee", "fees", "withdrawal", "deposit", "transfer",
        "inflation", "compound", "compound interest",
        // Comparative and analytical
        "category", "categories",
        "over budget", "under budget", "overspend", "underspend",
        // Advice-seeking phrases
    ];

    /// <summary>
    /// Keywords that strongly indicate the message is NOT about personal finance.
    /// </summary>
    private static readonly string[] OffTopicKeywords =
    [
        // Programming
        "function", "variable", "class", "method", "loop", "array", "string",
        "python", "javascript", "java", "c++", "c#", "rust", "go", "golang",
        "react", "angular", "vue", "node", "npm", "pip", "docker", "kubernetes",
        "git", "github", "stackoverflow", "algorithm", "debug", "compile",
        "api endpoint", "http request", "sql query", "database migration",
        "segmentation fault", "segfault",
        // Creative writing
        "write a story", "write a poem", "write a song", "creative writing",
        "fiction", "narrative", "plot", "character",
        "write me a poem", "write me a story",
        // Trivia and general knowledge
        "who won", "what year", "capital of", "population of",
        "president", "prime minister", "election", "politics", "political",
        // Health and medical
        "doctor", "medicine", "prescription", "symptom", "symptoms", "diagnosis",
        "treatment", "hospital", "clinic",
        // Legal
        "lawyer", "attorney", "lawsuit", "legal advice", "court", "judge",
        // Entertainment
        "movie", "tv show", "series", "episode", "actor", "actress",
        "game", "gaming", "playstation", "xbox", "nintendo",
        // Sports
        "football", "basketball", "baseball", "soccer", "hockey", "tennis",
        "score", "team", "player", "league",
        // Stock picking (specific, not general investing)
        "buy stock", "sell stock", "stock price", "ticker", "shares",
        "bitcoin", "crypto", "cryptocurrency", "ethereum", "nft",
        // General knowledge / science
        "quantum", "physics", "biology", "chemistry", "mathematics",
        "define",
    ];

    /// <summary>
    /// Common greetings and small talk patterns.
    /// </summary>
    private static readonly string[] GreetingPatterns =
    [
        "hi", "hello", "hey", "good morning", "good afternoon", "good evening",
        "howdy", "greetings", "welcome", "thanks", "thank you", "thx",
        "ty", "cheers", "appreciate it", "nice to meet you",
    ];

    /// <summary>
    /// Prompt injection patterns that should be treated as off-topic.
    /// </summary>
    private static readonly string[] InjectionPatterns =
    [
        "ignore previous instructions", "ignore all previous instructions", "ignore the above", "disregard previous",
        "system prompt", "you are now", "from now on you", "new instruction",
        "forget your instructions", "override your",
    ];

    /// <summary>
    /// Whether the text contains one of the prompt-injection patterns, ignoring case. This is the only check the
    /// Money Priorities Purpose gets: the keyword scope filter would refuse ordinary purposes.
    /// </summary>
    public static bool LooksLikePromptInjection(string text)
    {
        string lower = text.ToLowerInvariant();
        return InjectionPatterns.Any(pattern => lower.Contains(pattern, StringComparison.Ordinal));
    }

    /// <summary>
    /// Evaluates the user message and returns a canned response if the message is off-topic or a greeting.
    /// Returns null when the message is finance-related and should proceed to the LLM.
    /// </summary>
    public AiChatResponseDto? Evaluate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string lowerMessage = message.ToLowerInvariant().Trim();

        // Check message length limit
        if (message.Length > AiAssistantMessages.MaxMessageLength)
        {
            return new AiChatResponseDto
            {
                Response = "Your message is too long. Please keep it under "
                          + AiAssistantMessages.MaxMessageLength + " characters.",
                FixedText = ResultMessage.Entry(ResultMessageKeys.AiAssistant.TooLong,
                    "Your message is too long. Please keep it under {max} characters.",
                    ErrorArgs.Number("max", AiAssistantMessages.MaxMessageLength)),
                IsRefusal = true,
                Disclaimer = null,
                TimestampUtc = DateTime.UtcNow,
            };
        }

        // Check for prompt injection attempts
        if (LooksLikePromptInjection(lowerMessage))
        {
            return CreateRefusalResponse();
        }

        // Check for greetings / small talk
        if (IsGreeting(lowerMessage))
        {
            return CreateGreetingResponse();
        }

        // Check for off-topic keywords first (stronger signal)
        int offTopicScore = CountKeywordMatches(lowerMessage, OffTopicKeywords);
        int financeScore = CountKeywordMatches(lowerMessage, FinanceKeywords);

        // Mixed requests still contain an out-of-scope task; do not spend tokens on it.
        if (offTopicScore > 0)
        {
            return CreateRefusalResponse();
        }

        return financeScore > 0 ? null : CreateRefusalResponse();
    }

    /// <summary>
    /// Determines whether the message is a greeting or small talk.
    /// </summary>
    private static bool IsGreeting(string lowerMessage)
    {
        // Short messages that match a greeting pattern exactly or with minimal punctuation
        if (lowerMessage.Length < 30)
        {
            foreach (string pattern in GreetingPatterns)
            {
                if (lowerMessage == pattern || lowerMessage == pattern + "!" || lowerMessage == pattern + "?")
                {
                    return true;
                }
            }
        }

        // Greeting followed by non-finance content
        foreach (string pattern in GreetingPatterns)
        {
            if (lowerMessage.StartsWith(pattern + " ", StringComparison.Ordinal) ||
                lowerMessage.StartsWith(pattern + ",", StringComparison.Ordinal))
            {
                // Check if the rest contains finance keywords
                string rest = lowerMessage[pattern.Length..].Trim();
                if (rest.Length < 10)
                {
                    return true;
                }

                bool hasFinanceKeyword = FinanceKeywords.Any(kw => rest.Contains(kw));
                if (!hasFinanceKeyword)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Counts how many keywords from the given array appear in the message.
    /// </summary>
    private static int CountKeywordMatches(string lowerMessage, string[] keywords)
    {
        int count = 0;
        foreach (string keyword in keywords)
        {
            if (Regex.IsMatch(lowerMessage, @"(?<!\w)" + Regex.Escape(keyword) + @"(?!\w)",
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Creates a canned refusal response for off-topic queries.
    /// </summary>
    private static AiChatResponseDto CreateRefusalResponse()
    {
        return new AiChatResponseDto
        {
            Response = AiAssistantMessages.OffTopicRefusal,
            FixedText = ResultMessage.Entry(ResultMessageKeys.AiAssistant.OffTopic, AiAssistantMessages.OffTopicRefusal),
            IsRefusal = true,
            Disclaimer = null,
            TimestampUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a canned greeting acknowledgement response.
    /// </summary>
    private static AiChatResponseDto CreateGreetingResponse()
    {
        return new AiChatResponseDto
        {
            Response = AiAssistantMessages.GreetingAcknowledgement,
            FixedText = ResultMessage.Entry(ResultMessageKeys.AiAssistant.Greeting,
                AiAssistantMessages.GreetingAcknowledgement),
            IsRefusal = true,
            Disclaimer = null,
            TimestampUtc = DateTime.UtcNow,
        };
    }
}
