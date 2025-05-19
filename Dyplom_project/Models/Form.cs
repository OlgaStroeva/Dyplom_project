using System.Collections.Generic;

public class Form
{
    public int Id { get; set; }
    public List<string> Fields { get; set; } = new();
}
public class FormTemplate
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}
