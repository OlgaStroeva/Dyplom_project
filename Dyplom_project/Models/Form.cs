using System.Collections.Generic;

public class Form
{
    public int Id { get; set; }
    public List<FormField> Fields { get; set; } = new();
}

public class FormField
{
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
}

public class UpdateParticipantRequest
{
    public Dictionary<string, string> Data { get; set; } = new();
}