using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.Security;

public static class EmployeePasswordPolicy
{
    public static void Validate(string? password)
    {
        if (password is null || password.Length is < 12 or > 72
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Mật khẩu phải có 12–72 ký tự, ít nhất một chữ và một số.");
    }
}
