using Dyplom_project.Models;
using Microsoft.AspNetCore.Authorization;
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

        /*if (!user.IsEmailConfirmed)
        {
            return Unauthorized(new { message = "Email не подтверждён." });
        }*/

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
        
        if (user.PasswordResetAttempts >= 10)
        {
            return BadRequest(new { message = "Превышено допустимое количество запросов." });
        }

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
        if(CheckEmail(request.Email))
            return Conflict(new { message = "Недопустимый адрес электронной почты" });
        var existing = await _dbContext.GetUserByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new { message = "Пользователь с таким email уже существует." });

        var confirmationCode = Guid.NewGuid().ToString(); // Уникальный код

        var user = new User
        {
            Id = new Random().Next(10000, 99999),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CanBeStaff = true,
            IsEmailConfirmed = false,
            EmailConfirmationCode = confirmationCode
        };

        await _dbContext.CreateUserAsync(user);

        // Отправка письма
        var confirmationLink = $"http://158.160.171.159:3000/sign-in?confirm={confirmationCode}";
        await _emailService.SendEmailAsync(
            user.Email,
            "Подтверждение электронной почты",
            $"Для подтверждения перейдите по ссылке: <a href=\"{confirmationLink}\">{confirmationLink}</a>",
            true
        );

        return Ok(new { message = "Письмо с подтверждением отправлено на email." });
    }
    
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {
        var user = await _dbContext.GetUserByConfirmationCodeAsync(token);
        if (user == null)
            return NotFound(new { success = false, message = "Неверный код подтверждения." });

        if (user.IsEmailConfirmed)
            return Ok(new { success = true, message = "Почта уже подтверждена." });

        user.IsEmailConfirmed = true;
        user.EmailConfirmationCode = "";
        await _dbContext.UpdateUserAsync(user);
        
        return Ok(new { success = true, message = "Почта успешно подтверждена." });
    }
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetUserData()
    {
        var userIdClaim = User.FindFirst("userId");
        var userId = userIdClaim?.Value;
        Console.WriteLine(userId);
        var user = await _dbContext.GetUserByIdAsync(Convert.ToInt32(userId));
        if (user == null)
            return NotFound(new { message = "Пользователь не найден." });

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            user.CanBeStaff,
            user.IsEmailConfirmed
        });
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        var userId = userIdClaim?.Value;
        var user = await _dbContext.GetUserByIdAsync(Convert.ToInt32(userId));

        if (user == null || !user.VerifyPassword(request.OldPassword))
            return BadRequest(new { message = "Неверный старый пароль." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _dbContext.UpdateUserAsync(user);

        return Ok(new { message = "Пароль успешно обновлён." });
    }
    [HttpPut("change-name")]
    [Authorize]
    public async Task<IActionResult> ChangeName([FromBody] ChangeNameRequest request)
    {
        var userIdClaim = User.FindFirst("userId");
        var userId = userIdClaim?.Value;

        var user = await _dbContext.GetUserByIdAsync(Convert.ToInt32(userId));

        if (user == null)
            return NotFound(new { message = "Пользователь не найден." });

        user.Name = request.Name;
        await _dbContext.UpdateUserNameAsync(user);

        return Ok(new { message = "Имя успешно обновлено." });
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