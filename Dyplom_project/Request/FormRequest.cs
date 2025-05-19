using System.Collections.Generic;

public class FormRequest
{
    public List<string> Fields { get; set; } = new();
}

public class CreateFormRequest
{
    public int InvitationTemplateId { get; set; }
}