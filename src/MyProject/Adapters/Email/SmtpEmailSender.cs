using System.Net.Mail;
using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Email;

internal class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(config["Email:Host"], config.GetValue<int>("Email:Port"));
        var message = new MailMessage(config["Email:From"]!, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
