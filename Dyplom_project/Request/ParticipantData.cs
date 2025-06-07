using System.Collections.Generic;
public class ParticipantData
{
    public int Id { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public bool Attended { get; set; } // ✅ новое поле
    public string QrCode { get; set; } = string.Empty; // QR-код в виде Base64

}


public class AddParticipantRequest
{
    public List<Dictionary<string, string>> Data { get; set; } = new();
}
