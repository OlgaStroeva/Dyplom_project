namespace Dyplom_project.Models;

public interface IInvitationService
{
    Task<int> SendInvitationsAsync(int eventId);
}
