using Dyplom_project.Models;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtService _jwtService;

    private readonly IEmailService _emailService;

    public AuthController(ApplicationDbContext dbContext, JwtService jwtService, IEmailService emailService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _emailService = emailService;
    }
    
    /// <summary>
    /// Логин пользователя и получение JWT-токена.
    /// </summary>
    /// <param name="request">Email и пароль.</param>
    /// <returns>JWT-токен или сообщение об ошибке.</returns>
    [HttpPost("login")]
    [SwaggerOperation(Summary = "Авторизация пользователя", Description = "Позволяет войти в систему и получить JWT-токен.")]
    [SwaggerResponse(200, "Авторизация успешна, возвращает JWT-токен")]
    [SwaggerResponse(400, "Некорректные данные")]
    [SwaggerResponse(401, "Неправильный email или пароль")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email и пароль обязательны." });
        }

        var user = await _dbContext.GetUserByEmailAsync(request.Email);
        if (user == null || !user.VerifyPassword(request.Password))
        {
            return Unauthorized(new { message = "Неверный email или пароль." });
        }

        var token = _jwtService.GenerateToken(user);
        return Ok(new { token });
    }
    
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetApply request)
    {
        var user = await _dbContext.GetUserByResetTokenAsync(request.Token);
        if (user == null)
            return NotFound(new { message = "Неверный или истёкший токен." });

        // Проверка срока действия токена
        if (user.PasswordResetRequestedAt == null ||
            (DateTime.UtcNow - user.PasswordResetRequestedAt.Value).TotalMinutes > 20)
        {
            return BadRequest(new { message = "Срок действия ссылки истёк. Запросите новую." });
        }

        // Обновление пароля
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = "";
        user.PasswordResetAttempts = 0;
        user.PasswordResetRequestedAt = null;

        await _dbContext.UpdateUserAsync(user);

        return Ok(new { message = "Пароль успешно обновлён." });
    }

    
    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request)
    {
        var user = await _dbContext.GetUserByEmailAsync(request.Email);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден." });

        var now = DateTime.UtcNow;

        // Проверка количества попыток
        if (user.PasswordResetAttempts >= 10)
        {
            return BadRequest(new { message = "Превышено допустимое количество запросов." });
        }

        // Проверка интервала между запросами
        if (user.PasswordResetRequestedAt != null &&
            (now - user.PasswordResetRequestedAt.Value).TotalSeconds < 90)
        {
            return BadRequest(new { message = "Пожалуйста, подождите 1.5 минуты перед повторным запросом." });
        }

        // Генерация токена и обновление полей
        var resetToken = Guid.NewGuid().ToString();
        user.PasswordResetToken = resetToken;
        user.PasswordResetRequestedAt = now;
        user.PasswordResetAttempts++;

        await _dbContext.UpdateUserAsync(user);

        var link = $"{Request.Scheme}://{Request.Host}/reset-password?token={resetToken}";

        await _emailService.SendEmailAsync(
            user.Email,
            "Восстановление пароля",
            $"Для сброса пароля перейдите по ссылке (действует 20 минут): {link}"
        );

        return Ok(new { message = "Ссылка на восстановление пароля отправлена." });
    }


    /// <summary>
    /// Регистрирует нового пользователя.
    /// </summary>
    /// <param name="request">Данные пользователя (имя, email, пароль).</param>
    /// <returns>Сообщение об успешной регистрации или ошибке.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existing = await _dbContext.GetUserByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new { message = "Пользователь с таким email уже существует." });

        var confirmationCode = Guid.NewGuid().ToString(); // Уникальный код

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CanBeStaff = request.CanBeStaff,
            IsEmailConfirmed = false,
            EmailConfirmationCode = confirmationCode
        };

        await _dbContext.CreateUserAsync(user);

        // Отправка письма
        var confirmationLink = $"{Request.Scheme}://{Request.Host}/api/auth/confirm?code={confirmationCode}";
        await _emailService.SendEmailAsync(request.Email, "Подтверждение регистрации", $"Для подтверждения перейдите по ссылке: {confirmationLink}");

        return Ok(new { message = "Письмо с подтверждением отправлено на email." });
    }

    [HttpGet("confirm")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string code)
    {
        var user = await _dbContext.GetUserByConfirmationCodeAsync(code);
        if (user == null)
            return NotFound(new { message = "Неверный код подтверждения." });

        if (user.IsEmailConfirmed)
            return BadRequest(new { message = "Email уже подтверждён." });

        user.IsEmailConfirmed = true;
        user.EmailConfirmationCode = "";
        await _dbContext.UpdateUserAsync(user);

        return Ok(new { message = "Email подтверждён успешно!" });
    }


    bool CheckEmail(string email)
    {
        string[] availableEmails = { "gmail.com", "mail.ru", "yandex.ru", "duck.com", "kursksu.ru" };

        var parts = email.Split('@');

        if (parts.Length != 2) return true; 

        string domain = parts[1]; 

        return !availableEmails.Contains(domain);
    }


}