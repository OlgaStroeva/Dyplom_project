using System.Collections.Generic;

public class Form
{
    public int Id { get; set; }  
    public int EventId { get; set; } // ID мероприятия
    public List<string> Fields { get; set; } = new() { "Email" }; // Обязательное поле Email
}