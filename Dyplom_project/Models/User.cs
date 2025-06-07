
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
    

    // Проверка пароля при логине
    public bool VerifyPassword(string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }

}
