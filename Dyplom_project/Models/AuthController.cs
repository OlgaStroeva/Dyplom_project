using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtService _jwtService;

    public AuthController(ApplicationDbContext dbContext, JwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
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

    /// <summary>
    /// Регистрирует нового пользователя.
    /// </summary>
    /// <param name="request">Данные пользователя (имя, email, пароль).</param>
    /// <returns>Сообщение об успешной регистрации или ошибке.</returns>
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Регистрация нового пользователя", Description = "Создаёт нового пользователя в системе.")]
    [SwaggerResponse(200, "Регистрация успешна")]
    [SwaggerResponse(400, "Некорректные данные")]
    [SwaggerResponse(409, "Email уже используется")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email и пароль обязательны." });
        }
        if(CheckEmail(request.Email)) 
            return BadRequest(new { message = "Некорректные данные электронной почты."  });
        // Проверяем, есть ли уже такой email
        var existingUser = await _dbContext.GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Conflict(new { message = "Этот email уже используется." });
        }

        // Создаём нового пользователя
        var newUser = new User
        {
            Id = new Random().Next(10, 9999), // Генерируем временный ID (лучше использовать UUID)
            Name = request.Name,
            Email = request.Email
        };
        newUser.SetPassword(request.Password); // Хешируем пароль

        await _dbContext.CreateUserAsync(newUser);

        return Ok(new { message = "Регистрация успешна!" });
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