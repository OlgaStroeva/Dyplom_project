using System.Collections.Generic;

public class FormRequest
{
    public List<FormField> Fields { get; set; } = new();
}

public class CreateFormRequest
{
    public int InvitationTemplateId { get; set; }
}