namespace PersonalFinance.Web.Services;

public class BrevoOptions
{
    public string? ApiKey { get; set; }
    public string? SenderEmail { get; set; } = "bc723b001@smtp-brevo.com";
    public string? SenderName { get; set; } = "PersonalFinance";
    public string SmtpServer { get; set; } = "smtp-relay.brevo.com";
    public int Port { get; set; } = 587;
    public string? Login { get; set; } = "bc723b001@smtp-brevo.com";
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}
