using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[Route("api/staff")]
[ApiController]
public class StaffController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtService _jwtService;

    public StaffController(ApplicationDbContext dbContext, JwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }
    [HttpGet("find")]
    [Authorize]
    [SwaggerOperation(Summary = "Поиск пользователей по email", Description = "Позволяет организатору найти пользователей по части email.")]
    [SwaggerResponse(200, "Список пользователей найден")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    public async Task<IActionResult> FindUsers([FromQuery] string emailPart)
    {
        if (string.IsNullOrWhiteSpace(emailPart))
        {
            return BadRequest(new { message = "Введите часть email для поиска." });
        }

        var users = await _dbContext.FindUsersByEmailAsync(emailPart);
        return Ok(users);
    }

    [HttpPost("assign")]
    [Authorize]
    [SwaggerOperation(Summary = "Назначить сотрудника", Description = "Позволяет организатору назначить пользователя сотрудником мероприятия.")]
    [SwaggerResponse(200, "Сотрудник назначен")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [SwaggerResponse(403, "Нет прав для назначения")]
    [SwaggerResponse(404, "Мероприятие или пользователь не найден")]
    public async Task<IActionResult> AssignStaff([FromBody] AssignStaffRequest request)
    {
        
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        int organizerId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByIdAsync(request.EventId);
        if (eventData.CreatedBy != organizerId)
        {
            return Forbid();
        }

        if (eventData == null) return NotFound(new { message = "Мероприятие не найдено." });

        if (eventData.CreatedBy != organizerId) return Forbid();

        await _dbContext.AddStaffAsync(request.EventId, request.UserId);
        return Ok(new { message = "Сотрудник назначен!" });
    }

    [HttpDelete("remove")]
    [Authorize]
    [SwaggerOperation(Summary = "Снять сотрудника", Description = "Позволяет организатору снять сотрудника с мероприятия.")]
    [SwaggerResponse(200, "Сотрудник снят")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [SwaggerResponse(403, "Нет прав для снятия")]
    [SwaggerResponse(404, "Мероприятие или сотрудник не найден")]
    public async Task<IActionResult> RemoveStaff([FromBody] RemoveStaffRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        int organizerId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByIdAsync(request.EventId);
        if (eventData.CreatedBy != organizerId)
        {
            return Forbid();
        }

        if (eventData == null) return NotFound(new { message = "Мероприятие не найдено." });

        if (eventData.CreatedBy != organizerId) return Forbid();

        await _dbContext.RemoveStaffAsync(request.EventId, request.UserId);
        return Ok(new { message = "Сотрудник снят!" });
    }

    [HttpDelete("leave")]
    [Authorize]
    [SwaggerOperation(Summary = "Отказаться от мероприятия", Description = "Позволяет сотруднику удалить мероприятие из своего списка.")]
    [SwaggerResponse(200, "Мероприятие удалено")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [SwaggerResponse(404, "Мероприятие не найдено")]
    public async Task<IActionResult> LeaveEvent([FromBody] LeaveEventRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        int userId = int.Parse(userIdClaim.Value);
        await _dbContext.LeaveEventAsync(request.EventId, userId);
        return Ok(new { message = "Мероприятие удалено!" });
    }

}