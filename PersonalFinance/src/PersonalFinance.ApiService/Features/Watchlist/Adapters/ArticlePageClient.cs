using System.Text;

namespace PersonalFinance.ApiService.Features.Watchlist.Adapters;

/// <summary>
/// <see cref="IArticlePageClient"/> over HTTP, registered as a typed <see cref="HttpClient"/> with a short timeout. It
/// reads at most <see cref="MaxBytes"/> of an HTML page and keeps its paragraph text, capped at
/// <see cref="MaxTextLength"/> characters.
/// </summary>
internal sealed class ArticlePageClient : IArticlePageClient
{
    private const int MaxBytes = 1024 * 1024;
    private const int MaxTextLength = 6000;
    private const int MinParagraphLength = 40;

    private readonly HttpClient _httpClient;
    private readonly ILogger<ArticlePageClient> _logger;

    public ArticlePageClient(HttpClient httpClient, ILogger<ArticlePageClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> ReadTextAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Article page {Url} answered HTTP {StatusCode}", url, response.StatusCode);
                }

                return null;
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                return null;
            }

            string html = await ReadCappedAsync(response.Content, cancellationToken);
            string text = HtmlText.Paragraphs(html, MinParagraphLength);
            return text.Length == 0 ? null : HtmlText.Cap(text, MaxTextLength);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(ex, "Could not read the article page {Url}", url);
            }

            return null;
        }
    }

    /// <summary>The first <see cref="MaxBytes"/> of the body, decoded with the declared charset or UTF-8.</summary>
    private static async Task<string> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[16 * 1024];
        int read;
        while (buffer.Length < MaxBytes
               && (read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxBytes - buffer.Length)), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        Encoding encoding = Encoding.UTF8;
        string? charset = content.Headers.ContentType?.CharSet?.Trim('"');
        if (!string.IsNullOrEmpty(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                // An unknown charset: read as UTF-8.
            }
        }

        return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
