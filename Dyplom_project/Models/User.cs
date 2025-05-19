
using System.Security.Cryptography;
using System.Text;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool CanBeStaff { get; set; } = true;
    public bool IsEmailConfirmed { get; set; } = false;
    public string EmailConfirmationCode { get; set; } = "";
    public string PasswordResetToken { get; set; } = "";
    public DateTime? PasswordResetRequestedAt { get; set; }
    public int PasswordResetAttempts { get; set; } = 0;


    // Метод для хеширования пароля перед сохранением в БД
    public void SetPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        PasswordHash = Convert.ToBase64String(hash);
    }

    // Проверка пароля при логине
    public bool VerifyPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return PasswordHash == Convert.ToBase64String(hash);
    }
}
