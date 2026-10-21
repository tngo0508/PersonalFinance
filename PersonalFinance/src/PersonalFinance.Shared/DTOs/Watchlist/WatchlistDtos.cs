namespace PersonalFinance.Shared.DTOs.Watchlist;

public class SavedArticleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime SavedAtUtc { get; set; }
}

public class SaveArticleRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

public class SaveArticleResponseDto
{
    public bool Success { get; set; }
    public bool AlreadySaved { get; set; }
    public int Id { get; set; }
}

public class RemoveArticleResponseDto
{
    public bool Success { get; set; }
}

public class CheckArticleSavedResponseDto
{
    public bool IsSaved { get; set; }
}
