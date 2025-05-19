namespace Dyplom_project.Models;

public class InvitationService : IInvitationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;

    public InvitationService(ApplicationDbContext dbContext, IEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
    }

    public async Task<int> SendInvitationsAsync(int eventId)
    {
        var eventData = await _dbContext.GetEventByIdAsync(eventId);
        if (eventData == null)
            throw new Exception("Мероприятие не найдено.");

        var formId = await _dbContext.GetFormByEventIdAsync(eventId);
        if (formId == null)
            throw new Exception("Форма не найдена.");

        var participants = await _dbContext.GetAllParticipantDataAsync(formId.Id);

        int sentCount = 0;

        foreach (var p in participants)
        {
            if (!p.Data.TryGetValue("Email", out var email) || string.IsNullOrWhiteSpace(email))
                continue;

            if (!p.Data.TryGetValue("QrCode", out var qrCodeBase64) || string.IsNullOrWhiteSpace(qrCodeBase64))
                continue;

            var subject = $"Приглашение на \"{eventData.Name}\"";
            var bodyHtml = BuildHtmlEmailBody(eventData, qrCodeBase64);

            await _emailService.SendEmailAsync(email, subject, bodyHtml, isHtml: true);
            sentCount++;
        }

        return sentCount;
    }
    private string BuildHtmlEmailBody(Event eventData, string qrCodeBase64)
    {
        return $@"
        <p>Здравствуйте!</p>
        <p>Вы приглашены на мероприятие:</p>
        <ul>
            <li><strong>Название:</strong> {eventData.Name}</li>
            <li><strong>Дата и время:</strong> {eventData.DateTime}</li>
            <li><strong>Место:</strong> {eventData.Location}</li>
        </ul>
        <p>Пожалуйста, предъявите этот QR-код при входе:</p>
        <img src='data:image/png;base64,{qrCodeBase64}' alt='QR Code' />
        <p>До встречи!</p>";
    }


}
