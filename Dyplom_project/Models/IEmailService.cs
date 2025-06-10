namespace Dyplom_project.Models;

public interface IEmailService
{
    public Task SendEmailAsync(
        string toEmail, 
        string subject, 
        string body, 
        bool isHtml = false,
        string? fromEmail = null);
}
