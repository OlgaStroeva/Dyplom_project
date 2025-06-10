namespace Dyplom_project.Models;
// IInvitationService.cs
public interface IInvitationService
{
    Task<bool> SendInvitationsAsync(int participantId, int formId);
}
