using System.Net.Mail;

namespace PersonalFinance.Web.Services;

public interface ISmtpClient
{
    Task SendMailAsync(MailMessage message, CancellationToken cancellationToken = default);
}
