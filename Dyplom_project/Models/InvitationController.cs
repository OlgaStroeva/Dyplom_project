using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dyplom_project.Models;

[ApiController]
[Route("api/invitations")]
public class InvitationController : ControllerBase
{
    private readonly InvitationService _invitationService;

    public InvitationController(InvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost("send/{formId}/{participantId}")]
    [Authorize]
    public async Task<IActionResult> SendInvitation(int participantId, int formId)
    {
        try
        {
            Console.WriteLine($"Вызов SendInvitation для participantId={participantId}, formId={formId}");
        
            bool success = await _invitationService.SendInvitationsAsync(participantId, formId);
        
            Console.WriteLine($"Успешная отправка для participantId={participantId}");
            return Ok(new { 
                message = $"Приглашение отправлено участнику {participantId}",
                participantId,
                formId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка для participantId={participantId}: {ex}");
            return BadRequest(new { 
                message = ex.Message,
                participantId,
                formId
            });
        }
    }
}
