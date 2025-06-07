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

public class ChangeNameRequest
{
    public string Name { get; set; } = "";
}
public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}