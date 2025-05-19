using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dyplom_project.Models;

[ApiController]
[Route("api/invitations")]
public class InvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost("send/{eventId}")]
    [Authorize]
    public async Task<IActionResult> SendInvitations(int eventId)
    {
        try
        {
            int count = await _invitationService.SendInvitationsAsync(eventId);
            return Ok(new { message = $"Приглашения отправлены {count} участникам." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
