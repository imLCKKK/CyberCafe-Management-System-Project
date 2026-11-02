namespace CyberManagement.Application.Interfaces.Validation;

public interface IRequestValidator<in TRequest>
{
    void Validate(TRequest request);
}
