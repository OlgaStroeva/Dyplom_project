using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

[Route("api/events")]
[ApiController]
[Authorize] // Требуется JWT-токен
public class EventController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public EventController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Создаёт новое мероприятие.
    /// </summary>
    /// <param name="request">Название мероприятия.</param>
    /// <returns>Сообщение об успешном создании.</returns>
    [HttpPost("create")]
    [SwaggerOperation(Summary = "Создать мероприятие", Description = "Позволяет авторизованному пользователю создать новое мероприятие.")]
    [SwaggerResponse(200, "Мероприятие создано")]
    [SwaggerResponse(400, "Некорректные данные")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Название мероприятия обязательно." });
        }

        // Получаем ID авторизованного пользователя из JWT-токена
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null)
        {
            return Unauthorized(new { message = "Не удалось определить пользователя." });
        }

        int userId = int.Parse(userIdClaim.Value);

        // Создаём мероприятие
        var newEvent = new Event
        {
            Id = new Random().Next(10000, 99999), // Генерируем ID (лучше UUID)
            Name = request.Name,
            CreatedBy = userId
        };

        await _dbContext.CreateEventAsync(newEvent);

        return Ok(new { message = "Мероприятие создано!", eventId = newEvent.Id });
    }
    
    /// <summary>
    /// Редактирует мероприятие.
    /// </summary>
    /// <param name="eventId">ID мероприятия.</param>
    /// <param name="request">Новые данные.</param>
    /// <returns>Сообщение об успешном редактировании.</returns>
    [HttpPut("update/{eventId}")]
    [SwaggerOperation(Summary = "Редактировать мероприятие", Description = "Позволяет автору мероприятия редактировать его.")]
    [SwaggerResponse(200, "Мероприятие обновлено")]
    [SwaggerResponse(400, "Некорректные данные")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [SwaggerResponse(403, "Нет прав для редактирования")]
    [SwaggerResponse(404, "Мероприятие не найдено")]
    [Authorize]
    public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] UpdateEventRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        int userId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByIdAsync(eventId);

        if (eventData == null)
            return NotFound(new { message = "Мероприятие не найдено." });

        if (eventData.CreatedBy != userId)
            return Forbid();

        await _dbContext.UpdateEventAsync(eventId, request);
        return Ok(new { message = "Информация о мероприятии обновлена." });
    }

    
    /// <summary>
    /// Получает мероприятие по ID.
    /// </summary>
    /// <param name="eventId">ID мероприятия.</param>
    /// <returns>Данные о мероприятии.</returns>
    [HttpGet("{eventId}")]
    [SwaggerOperation(Summary = "Получить мероприятие", Description = "Возвращает информацию о мероприятии по его ID.")]
    [SwaggerResponse(200, "Мероприятие найдено")]
    [SwaggerResponse(404, "Мероприятие не найдено")]
    public async Task<IActionResult> GetEventById(int eventId)
    {
        var eventData = await _dbContext.GetEventByIdAsync(eventId);
        if (eventData == null)
        {
            return NotFound(new { message = "Мероприятие не найдено." });
        }

        return Ok(eventData);
    }
    
    /// <summary>
    /// Получает список всех мероприятий пользователя.
    /// </summary>
    /// <returns>Список мероприятий.</returns>
    [HttpGet("my-events")]
    [SwaggerOperation(Summary = "Получить все мероприятия пользователя", Description = "Возвращает список мероприятий, созданных авторизованным пользователем.")]
    [SwaggerResponse(200, "Список мероприятий получен")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [Authorize]
    public async Task<IActionResult> GetUserEvents()
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null)
        {
            return Unauthorized(new { message = "Не удалось определить пользователя." });
        }

        int userId = int.Parse(userIdClaim.Value);
        var events = await _dbContext.GetUserEventsAsync(userId);

        return Ok(events);
    }
    [Authorize]
    [HttpDelete("delete/{eventId}")]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null)
            return Unauthorized(new { message = "Пользователь не авторизован." });

        int userId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByIdAsync(eventId);
        if (eventData == null)
            return NotFound(new { message = "Мероприятие не найдено." });

        if (eventData.CreatedBy != userId)
            return Forbid();

        // Удаляем, если статус "finished"
        if (eventData.Status == "finished")
        {
            await _dbContext.DeleteEventAsync(eventId);
            return Ok(new { message = "Мероприятие успешно удалено (статус: finished)." });
        }

        // Проверяем, были ли приглашения отправлены
        var form = await _dbContext.GetFormByEventIdAsync(eventId);
        if (form != null)
        {
            var participants = await _dbContext.GetAllParticipantDataAsync(form.Id);
            if (participants.Any(p => p.Invited))
                return BadRequest(new { message = "Нельзя удалить мероприятие — приглашения уже были отправлены." });
        }

        await _dbContext.DeleteEventAsync(eventId);
        return Ok(new { message = "Мероприятие удалено (без отправленных приглашений)." });
    }

    
    
    [SwaggerOperation(Summary = "Обновить статус мероприятия")]
    [SwaggerResponse(200, "Статус обновлен")]
    [SwaggerResponse(400, "Некорректный статус")]
    [SwaggerResponse(404, "Мероприятие не найдено")]
    [Authorize]
    [HttpPut("{eventId}/status")]
    public async Task<IActionResult> UpdateEventStatus(
        int eventId, 
        [FromBody] UpdateEventStatusRequest request)
    {
        // Валидация статуса
        var allowedStatuses = new[] { "upcoming", "in_progress", "finished"};
        if (!allowedStatuses.Contains(request.Status?.ToLower()))
        {
            return BadRequest("Недопустимый статус мероприятия");
        }

        try
        {
            await _dbContext.UpdateEventStatusAsync(eventId, request.Status!);
            return Ok(new { Message = "Статус мероприятия обновлен" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Ошибка сервера"});
        }
    }
}
