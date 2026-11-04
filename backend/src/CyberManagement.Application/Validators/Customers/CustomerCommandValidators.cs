using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Validators.Customers;

internal static class CustomerRules
{
    public const decimal MinTopUp = 10_000m;
    public const decimal MaxTopUp = 2_000_000m;
    public const decimal TopUpStep = 1_000m;

    private static readonly Regex PhonePattern = new("^0[0-9]{9,10}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Name(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150 || name.Any(char.IsControl))
            throw new ValidationException("Tên phải từ 1 đến 150 ký tự và không chứa ký tự điều khiển.");
    }

    public static void Phone(string? value)
    {
        if (value is null || !PhonePattern.IsMatch(value.Trim()))
            throw new ValidationException("Số điện thoại phải bắt đầu bằng 0 và gồm 10-11 chữ số.");
    }

    public static void Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var email = value.Trim();
        if (email.Length > 150 || !EmailPattern.IsMatch(email))
            throw new ValidationException("Email không hợp lệ hoặc dài quá 150 ký tự.");
    }
}

public sealed class CreateCustomerCommandValidator : IRequestValidator<CreateCustomerCommand>
{
    public void Validate(CreateCustomerCommand request)
    {
        CustomerRules.Name(request.FullName);
        CustomerRules.Phone(request.Phone);
        CustomerRules.Email(request.Email);
        if (request.InitialPassword is null || request.InitialPassword.Length is < 8 or > 128)
            throw new ValidationException("Mật khẩu phải từ 8 đến 128 ký tự.");
    }
}

public sealed class UpdateCustomerCommandValidator : IRequestValidator<UpdateCustomerCommand>
{
    public void Validate(UpdateCustomerCommand request)
    {
        CustomerRules.Name(request.FullName);
        CustomerRules.Phone(request.Phone);
        CustomerRules.Email(request.Email);
    }
}

public sealed class ChangeCustomerStatusCommandValidator : IRequestValidator<ChangeCustomerStatusCommand>
{
    public void Validate(ChangeCustomerStatusCommand request)
    {
        if (request.NewStatus is not ("Active" or "Locked"))
            throw new ValidationException("Trạng thái phải là Active hoặc Locked.");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 255)
            throw new ValidationException("Phải nhập lý do khóa/mở khóa (tối đa 255 ký tự).");
    }
}

public sealed class StaffTopUpCommandValidator : IRequestValidator<StaffTopUpCommand>
{
    public void Validate(StaffTopUpCommand request)
    {
        if (request.Amount < CustomerRules.MinTopUp || request.Amount > CustomerRules.MaxTopUp
            || request.Amount % CustomerRules.TopUpStep != 0)
            throw new ValidationException(
                $"Số tiền nạp phải từ {CustomerRules.MinTopUp:N0} đến {CustomerRules.MaxTopUp:N0} VND và là bội số của {CustomerRules.TopUpStep:N0}.");
        if (request.CollectionMethod is not ("Cash" or "BankTransfer"))
            throw new ValidationException("Hình thức thu tiền phải là Cash hoặc BankTransfer.");
    }
}
