namespace Dyplom_project.Models;

public class ParticipantData
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}
