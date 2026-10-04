namespace PersonalFinance.Web.Services;

public class BrevoOptions
{
    public string? ApiKey { get; set; }
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; } = "PersonalFinance";
}
