using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Drawing;
using System.Drawing.Imaging;

[Route("api/forms")]
[ApiController]
[Authorize] // Требует JWT-токен
public class FormController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public FormController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Создаёт шаблон анкеты для мероприятия.
    /// </summary>
    /// <param name="eventId">ID мероприятия.</param>
    /// <returns>Сообщение об успешном создании.</returns>
    [HttpPost("create/{eventId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Создать шаблон анкеты", Description = "Создаёт шаблон анкеты с полем Email для мероприятия.")]
    [SwaggerResponse(200, "Шаблон анкеты создан")]
    [SwaggerResponse(400, "Анкета уже существует")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    [SwaggerResponse(403, "Нет прав для редактирования")]
    [SwaggerResponse(404, "Мероприятие не найдено")]
    public async Task<IActionResult> CreateForm(int eventId)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null)
        {
            return Unauthorized(new { message = "Не удалось определить пользователя." });
        }

        int userId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByIdAsync(eventId);

        if (eventData == null)
        {
            return NotFound(new { message = "Мероприятие не найдено." });
        }

        if (eventData.CreatedBy != userId)
        {
            return Forbid();
        }

        if (await _dbContext.EventHasFormAsync(eventId))
        {
            return BadRequest(new { message = "Анкета уже существует для данного мероприятия." });
        }

        int invitationTemplateId = await _dbContext.CreateFormAsync(eventId);

        return Ok(new { message = "Шаблон анкеты создан!", invitationTemplateId });
    }


    
    /// <summary>
    /// Редактирует шаблон анкеты.
    /// </summary>
    /// <param name="formId">ID шаблона анкеты.</param>
    /// <param name="request">Список новых полей.</param>
    /// <returns>Сообщение об успешном редактировании.</returns>
    [HttpPut("update/{formId}")]
    [SwaggerOperation(Summary = "Редактировать шаблон анкеты", Description = "Позволяет добавить или удалить поля в анкете.")]
    [SwaggerResponse(200, "Шаблон анкеты обновлён")]
    [SwaggerResponse(401, "Неавторизованный запрос")]
    public async Task<IActionResult> UpdateForm(int formId, [FromBody] FormRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null)
        {
            return Unauthorized(new { message = "Не удалось определить пользователя." });
        }
        var eventData = await _dbContext.GetEventByFormIdAsync(formId); 
        int userId = int.Parse(userIdClaim.Value);
        if (eventData == null || eventData.CreatedBy != userId)
        {
            return Forbid();
        }


        if (!request.Fields.Contains("Email"))
        {
            return BadRequest(new { message = "Поле Email обязательно." });
        }

        await _dbContext.UpdateFormAsync(formId, request.Fields);

        return Ok(new { message = "Шаблон анкеты обновлён!" });
    }
    
    
    [HttpDelete("delete/{formId}")]
    [SwaggerOperation(Summary = "Удалить шаблон анкеты", Description = "Позволяет удалить шаблон анкеты.")]
    [SwaggerResponse(200, "Шаблон анкеты удалён")]
    [SwaggerResponse(404, "Шаблон анкеты удалён")]
    [Authorize]
    public async Task<IActionResult> DeleteForm(int formId)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        int userId = int.Parse(userIdClaim.Value);
        var eventData = await _dbContext.GetEventByFormIdAsync(formId);

        if (eventData == null)
        {
            return NotFound(new { message = "Анкета или мероприятие не найдено." });
        }

        if (eventData.CreatedBy != userId)
        {
            return Forbid();
        }

        await _dbContext.DeleteFormAsync(formId, eventData.Id);
        return Ok(new { message = "Шаблон анкеты удалён!" });
    }

    [HttpGet("download/{formId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Скачать шаблон анкеты", Description = "Генерирует и скачивает XLSX-файл с заголовками анкеты.")]
    [SwaggerResponse(200, "Файл сгенерирован")]
    [SwaggerResponse(404, "Анкета не найдена")]
    public async Task<IActionResult> DownloadFormTemplate(int formId)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        var eventData = await _dbContext.GetEventByFormIdAsync(formId);
        if (eventData == null) return NotFound(new { message = "Анкета не найдена." });

        var xlsxFile = await _dbContext.GenerateFormTemplateXlsx(formId);
        return File(xlsxFile, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "form_template.xlsx");
    }

    [HttpPost("add-participant/{formId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Добавить участника", Description = "Позволяет организатору или сотруднику добавить данные участника.")]
    [SwaggerResponse(200, "Участник добавлен")]
    [SwaggerResponse(400, "Некорректные данные")]
    [SwaggerResponse(404, "Анкета не найдена")]
    public async Task<IActionResult> AddParticipant(int formId, [FromBody] AddParticipantRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        var eventData = await _dbContext.GetEventByFormIdAsync(formId);
        if (eventData == null) return NotFound(new { message = "Анкета не найдена." });

        var errors = await _dbContext.AddParticipantDataAsync(formId, request.Data);
        if (errors.Count > 0)
        {
            return BadRequest(new { message = "Ошибка в данных.", errors });
        }

        return Ok(new { message = "Участник добавлен!" });
    }
    
    [HttpPost("upload/{formId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Загрузить список приглашённых", Description = "Позволяет загрузить XLSX-файл с данными участников.")]
    [SwaggerResponse(200, "Файл обработан")]
    [SwaggerResponse(400, "Некорректный файл")]
    [SwaggerResponse(404, "Анкета не найдена")]
    public async Task<IActionResult> UploadParticipants(int formId, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Файл не загружен." });

        var userIdClaim = User.FindFirst("userId");
        if (userIdClaim == null) return Unauthorized();

        var eventData = await _dbContext.GetEventByFormIdAsync(formId);
        if (eventData == null) return NotFound(new { message = "Анкета не найдена." });

        var errors = await _dbContext.ParseXlsxParticipants(formId, file);
        return Ok(new { message = "Файл обработан.", errors });
    }
    
    [HttpPost("add-qr/{participantId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Добавить QR-код", Description = "Позволяет организатору или сотруднику добавить QR-код к участнику.")]
    [SwaggerResponse(200, "QR-код добавлен")]
    [SwaggerResponse(400, "Некорректное изображение")]
    [SwaggerResponse(404, "Участник не найден")]
    public async Task<IActionResult> AddQrCodeToParticipant(int participantId, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Файл не загружен." });

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
    
        using var image = Image.FromStream(memoryStream);
        using var outputStream = new MemoryStream();
        image.Save(outputStream, ImageFormat.Png);
    
        string qrCodeBase64 = Convert.ToBase64String(outputStream.ToArray());

        bool updated = await _dbContext.AddQrCodeToParticipantAsync(participantId, qrCodeBase64);
        if (!updated) return NotFound(new { message = "Участник не найден." });

        return Ok(new { message = "QR-код добавлен!" });
    }

}