public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageBase64 { get; set; } = "";
    public int CreatedBy { get; set; }

    public string? DateTime { get; set; } = "";
    public string? Category { get; set; } = null;
    public string? Location { get; set; } = ""; 
    public string? Status { get; set; } = "upcoming";
}
