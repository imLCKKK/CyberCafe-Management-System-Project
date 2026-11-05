namespace CyberManagement.Application.Exceptions;

// Tài nguyên không tồn tại; API ánh xạ sang 404 thay vì coi là request sai định dạng.
public class EntityNotFoundException : KeyNotFoundException
{
    public EntityNotFoundException(string message) : base(message) { }
}
