public class EventRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateEventRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageBase64 { get; set; }
    public string? DateTime { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }
}

public class UpdateEventStatusRequest
{
    public string Status { get; set; }
}