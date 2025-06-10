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

    public async Task SendEmailAsync(
        string toEmail, 
        string subject, 
        string body, 
        bool isHtml = false,
        string? fromEmail = null) // Опциональная переопределяемая почта отправителя
    {
        var message = new MimeMessage();
    
        // Устанавливаем отправителя (или дефолтного)
        message.From.Add(MailboxAddress.Parse(
            fromEmail ?? _config["Email:SenderEmail"]
        ));
    
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = isHtml 
            ? new TextPart("html") { Text = body } 
            : new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
    
        await client.ConnectAsync(
            _config["Email:SmtpHost"],
            int.Parse(_config["Email:SmtpPort"]),
            SecureSocketOptions.StartTlsWhenAvailable
        );

        // Аутентификация с Яндкс-аккаунтом
        await client.AuthenticateAsync(
            _config["Email:SenderEmail"], // Всегда используем основной аккаунт для аутентификации
            _config["Email:SenderPassword"]
        );

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }


}

