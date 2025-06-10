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
    
    public async Task<bool> SendInvitationsAsync(int participantId, int FormId)
    {
        // 1. Получаем данные участника
        var participant = await _dbContext.GetParticipantByIdAsync(participantId);
        if (participant == null)
            throw new Exception("Участник не найден.");
        
        // 2. Проверяем наличие email
        if (participant.Data["Email"] == null)
            throw new Exception("У участника не указан email");
        
        if (participant.qrCode == "")
            throw new Exception("Не добавлен QR код");
        

        // 3. Получаем данные события
        var eventData = await _dbContext.GetEventByFormIdAsync(FormId);
        if (eventData == null)
            throw new Exception("Мероприятие не найдено");
        
            var subject = $"Приглашение на \"{eventData.Name}\"";
            //var qrBytes = Convert.FromBase64String(participant.qrCode);
            var bodyHtml = BuildHtmlEmailBody(eventData, participant.qrCode);


            await _emailService.SendEmailAsync(participant.Data["Email"], subject, bodyHtml, isHtml: true);
            await _dbContext.UpdateParticipantInvitationAsync(participantId);
        return true;
    }
    private string BuildHtmlEmailBody(Event eventData, string qrCodeBase64)
    {
        // Убираем пробелы и переносы строк, если есть
        qrCodeBase64 = qrCodeBase64.Replace("\r", "").Replace("=\n", "").Replace("/-", "").Trim().Substring(0, qrCodeBase64.Length - 1);
        Console.WriteLine(qrCodeBase64);
        return $@"
        <html>
        <body style='font-family: Arial, sans-serif; color: #2d3748;'>
            <p>Здравствуйте!</p>
            <p>Вы приглашены на мероприятие:</p>
            <ul>
                <li><strong>Название:</strong> {eventData.Name}</li>
                <li>{eventData.Description}</li>
                <li><strong>Дата и время:</strong> {eventData.DateTime}</li>
                <li><strong>Место:</strong> {eventData.Location}</li>
            </ul>
            <p>Пожалуйста, предъявите этот QR-код при входе:</p>
            <p>
<div style='display: block; font-size: 0; line-height: 0;'>
                <img src=""data:image/png;base64,{qrCodeBase64}"" 
                     alt=""QR Code"" 
                     style=""width: 200px; height: 200px; border: 1px solid #e2e8f0; display: block;"" />
</div>
            </p>
            <p>До встречи!</p>
        </body>
        </html>";
    }


}
