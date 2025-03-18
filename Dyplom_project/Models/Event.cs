public class Event
{
    public int Id { get; set; }  
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; // Описание (до 10 000 символов)
    public string ImageUrl { get; set; } = string.Empty; // Ссылка на изображение
    public int InvitationTemplateId { get; set; } // ID шаблона приглашения
    public int CreatedBy { get; set; } // ID пользователя, который создал мероприятие
}

public class UpdateEventRequest
{
    public string Description { get; set; } = string.Empty; // Описание (до 10 000 символов)
    public string ImageUrl { get; set; } = string.Empty; // Ссылка на изображение
    public int InvitationTemplateId { get; set; } // ID шаблона приглашения
}
