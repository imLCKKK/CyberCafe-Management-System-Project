using CyberManagement.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace CyberManagement.API.ExceptionHandling;

// Ánh xạ lỗi Application sang HTTP; lỗi hệ thống chỉ ghi log, không lộ chi tiết nội bộ cho client.
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;

        var status = exception switch
        {
            EntityNotFoundException => StatusCodes.Status404NotFound,
            BusinessRuleException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => context.User.Identity?.IsAuthenticated == true
                ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };
        var title = status switch
        {
            400 => "Dữ liệu không hợp lệ",
            401 => "Chưa đăng nhập",
            403 => "Không có quyền truy cập",
            404 => "Không tìm thấy dữ liệu",
            409 => "Không thể thực hiện thao tác",
            _ => "Lỗi hệ thống"
        };
        if (status == 500)
            logger.LogError(exception, "Lỗi xử lý API {Path}; TraceId: {TraceId}", context.Request.Path, context.TraceIdentifier);

        await Results.Problem(statusCode: status, title: title,
            detail: status == 500 ? "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." : exception.Message,
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }
}
