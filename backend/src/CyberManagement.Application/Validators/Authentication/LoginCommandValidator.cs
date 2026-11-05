using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Validators.Authentication;

public sealed class LoginCommandValidator : IRequestValidator<LoginCommand>
{
    public void Validate(LoginCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length > 50)
        {
            throw new ValidationException("Tên đăng nhập là bắt buộc và không được vượt quá 50 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 256)
        {
            throw new ValidationException("Mật khẩu là bắt buộc và không được vượt quá 256 ký tự.");
        }
    }
}
