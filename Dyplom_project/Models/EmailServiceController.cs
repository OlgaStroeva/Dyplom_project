using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Dyplom_project.Models;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_config["Email:Sender"]));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = isHtml
            ? new TextPart("html") { Text = body }
            : new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_config["Email:SmtpHost"], int.Parse(_config["Email:SmtpPort"]), SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_config["Email:Sender"], _config["Email:Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

