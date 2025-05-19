public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public bool CanBeStaff { get; set; } = true;
}
public class PasswordResetRequest
{
    public string Email { get; set; } = "";
}

public class PasswordResetApply
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
