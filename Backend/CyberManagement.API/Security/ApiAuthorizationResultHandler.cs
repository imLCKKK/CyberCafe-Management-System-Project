using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace CyberManagement.API.Security;

// Trả lỗi JSON thống nhất khi chưa đăng nhập hoặc thiếu quyền.
// Chưa có cơ chế cấp token/cookie trong đợt này nên không chuyển hướng sang trang đăng nhập.
public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var status = context.User.Identity?.IsAuthenticated == true
            ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        await Results.Problem(statusCode: status,
            title: status == 401 ? "Chưa đăng nhập" : "Không có quyền truy cập",
            detail: status == 401 ? "Vui lòng đăng nhập để sử dụng API." : "Tài khoản không có quyền thực hiện thao tác này.",
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
    }
}
