public class AssignStaffRequest
{
    public int EventId { get; set; }
    public int UserId { get; set; }
}

public class RemoveStaffRequest
{
    public int EventId { get; set; }
    public int UserId { get; set; }
}

public class LeaveEventRequest
{
    public int EventId { get; set; }
}
