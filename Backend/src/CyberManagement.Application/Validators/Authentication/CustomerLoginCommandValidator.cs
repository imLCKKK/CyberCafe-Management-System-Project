using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Validators.Authentication;

public sealed class CustomerLoginCommandValidator : IRequestValidator<CustomerLoginCommand>
{
    public void Validate(CustomerLoginCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Trim().Length > 20)
        {
            throw new ValidationException("Số điện thoại là bắt buộc và không được vượt quá 20 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 256)
        {
            throw new ValidationException("Mật khẩu là bắt buộc và không được vượt quá 256 ký tự.");
        }
    }
}
